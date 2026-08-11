using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.HotUpdate;
using UnityEditor;
using UnityEngine;

public static class BuildCommand
{
        public static string HybridCLRBuildCacheDir => Application.dataPath + "/HybridCLRBuildCache";

        public static string AssetBundleOutputDir => $"{HybridCLRBuildCacheDir}/AssetBundleOutput";

        public static string AssetBundleSourceDataTempDir => $"{HybridCLRBuildCacheDir}/AssetBundleSourceData";


        public static string GetAssetBundleOutputDirByTarget(BuildTarget target)
        {
            return $"{AssetBundleOutputDir}/{target}";
        }

        public static string GetAssetBundleTempDirByTarget(BuildTarget target)
        {
            return $"{AssetBundleSourceDataTempDir}/{target}";
        }

        public static string ToRelativeAssetPath(string s)
        {
            return s.Substring(s.IndexOf("Assets/"));
        }

        /// <summary>
        /// 将HotFix.dll和HotUpdatePrefab.prefab打入common包.
        /// 将HotUpdateScene.unity打入scene包.
        /// </summary>
        /// <param name="tempDir"></param>
        /// <param name="outputDir"></param>
        /// <param name="target"></param>
        private static void BuildAssetBundles(string tempDir, string outputDir, BuildTarget target)
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outputDir);
            
            List<AssetBundleBuild> abs = new List<AssetBundleBuild>();

            {
                var prefabAssets = new List<string>();
                string testPrefab = $"{Application.dataPath}/Prefabs/HotUpdatePrefab.prefab";
                prefabAssets.Add(testPrefab);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                abs.Add(new AssetBundleBuild
                {
                    assetBundleName = "prefabs",
                    assetNames = prefabAssets.Select(s => ToRelativeAssetPath(s)).ToArray(),
                });
            }

            BuildPipeline.BuildAssetBundles(outputDir, abs.ToArray(), BuildAssetBundleOptions.None, target);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        public static void BuildAssetBundleByTarget(BuildTarget target)
        {
            BuildAssetBundles(GetAssetBundleTempDirByTarget(target), GetAssetBundleOutputDirByTarget(target), target);
        }

        [MenuItem("HybridCLR/Build/BuildAssetsAndCopyToCode")]
        public static void BuildAndCopyABAOTHotUpdateDlls()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            //BuildAssetBundleByTarget(target);
            CompileDllCommand.CompileDll(target);
            CopyABAOTHotUpdateDlls(target);
        }
        [MenuItem("HybridCLR/检测AOT是否缺失")]
        public static bool CheckAccessMissingMetadata()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            // aotDir指向 构建主包时生成的裁剪aot dll目录，而不是最新的SettingsUtil.GetAssembliesPostIl2CppStripDir(target)目录。
            // 一般来说，发布热更新包时，由于中间可能调用过generate/all，SettingsUtil.GetAssembliesPostIl2CppStripDir(target)目录中包含了最新的aot dll，
            // 肯定无法检查出类型或者函数裁剪的问题。
            // 需要在构建完主包后，将当时的aot dll保存下来，供后面补充元数据或者裁剪检查。
            //string aotDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string aotDir = Path.Combine(projectRoot, "AOTBackUp");
            // 第2个参数hotUpdateAssNames为热更新程序集列表。对于旗舰版本，该列表需要包含DHE程序集，即SettingsUtil.HotUpdateAndDHEAssemblyNamesIncludePreserved。
            var checker = new MissingMetadataChecker(aotDir, SettingsUtil.HotUpdateAssemblyNamesIncludePreserved);

            string hotUpdateDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                string dllPath = $"{hotUpdateDir}/{dll}";
                bool notAnyMissing = checker.Check(dllPath);
                if (!notAnyMissing)
                {
                    // DO SOMETHING
                    Debug.LogError("有缺失，需要重新打包");
                    return true;
                }
            }
            Debug.LogError("检测AOT是否缺失完毕");
            return false;
        }
        [MenuItem("HybridCLR/备份AOT")]
        static void BackUpAot()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string aotDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);

            string subFold = "sub";
            int lastSlashIndex = aotDir.LastIndexOf('/');
            if (lastSlashIndex != -1)
            {
                subFold = aotDir.Substring(lastSlashIndex + 1); // 从反斜杠后的字符开始截取
            }
        
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string destDir = Path.Combine(projectRoot, $"AOTBackUp/{subFold}");
        
            CopyDirectory(aotDir, destDir);
            Debug.LogError("备份AOT数据完毕！！！");
        }
        
        // 递归复制文件夹及其内容
        static void CopyDirectory(string sourceDir, string destDir)
        {
            // 如果目标文件夹不存在，则创建它
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // 复制源文件夹中的文件
            string[] files = Directory.GetFiles(sourceDir);
            foreach (string file in files)
            {
                // 获取文件的文件名并拼接到目标文件夹路径
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);

                // 复制文件
                File.Copy(file, destFile, true);  // 设置为true以覆盖同名文件
            }

            // 递归复制源文件夹中的子文件夹
            string[] directories = Directory.GetDirectories(sourceDir);
            foreach (string directory in directories)
            {
                string dirName = Path.GetFileName(directory);
                string newDestDir = Path.Combine(destDir, dirName);
                CopyDirectory(directory, newDestDir);  // 递归调用复制子文件夹
            }
        }

        public static void CopyABAOTHotUpdateDlls(BuildTarget target)
        {
            //CopyAssetBundlesToStreamingAssets(target);
            //CopyAOTAssembliesToStreamingAssets();
            CopyAOTAssembliesToCode();
            CopyHotUpdateAssembliesToCode();
            //CopyHotUpdateAssembliesToStreamingAssets();
        }


        //[MenuItem("HybridCLR/Build/BuildAssetbundle")]
        public static void BuildSceneAssetBundleActiveBuildTargetExcludeAOT()
        {
            BuildAssetBundleByTarget(EditorUserBuildSettings.activeBuildTarget);
        }

        public static void CopyAOTAssembliesToStreamingAssets()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            string aotAssembliesSrcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            string aotAssembliesDstDir = Application.streamingAssetsPath;

            foreach (var dll in SettingsUtil.AOTAssemblyNames)
            {
                string srcDllPath = $"{aotAssembliesSrcDir}/{dll}.dll";
                if (!File.Exists(srcDllPath))
                {
                    Debug.LogError($"ab中添加AOT补充元数据dll:{srcDllPath} 时发生错误,文件不存在。裁剪后的AOT dll在BuildPlayer时才能生成，因此需要你先构建一次游戏App后再打包。");
                    continue;
                }
                string dllBytesPath = $"{aotAssembliesDstDir}/{dll}.dll.bytes";
                File.Copy(srcDllPath, dllBytesPath, true);
                Debug.Log($"[CopyAOTAssembliesToStreamingAssets] copy AOT dll {srcDllPath} -> {dllBytesPath}");
            }
        }
        
        public static void CopyAOTAssembliesToCode()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            string aotAssembliesSrcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            string aotAssembliesDstDir = Application.dataPath + "/Res/Code";

            foreach (var dll in SettingsUtil.AOTAssemblyNames)
            {
                string srcDllPath = $"{aotAssembliesSrcDir}/{dll}.dll";
                if (!File.Exists(srcDllPath))
                {
                    Debug.LogError($"ab中添加AOT补充元数据dll:{srcDllPath} 时发生错误,文件不存在。裁剪后的AOT dll在BuildPlayer时才能生成，因此需要你先构建一次游戏App后再打包。");
                    continue;
                }
                string dllBytesPath = $"{aotAssembliesDstDir}/{dll}.dll.bytes";
                File.Copy(srcDllPath, dllBytesPath, true);
                Debug.Log($"[CopyAOTAssembliesToStreamingAssets] copy AOT dll {srcDllPath} -> {dllBytesPath}");
            }
        }

        public static void CopyHotUpdateAssembliesToCode()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;

            string hotfixDllSrcDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            string hotfixAssembliesDstDir = Application.dataPath + "/Res/Code";
            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                string dllPath = $"{hotfixDllSrcDir}/{dll}";
                string dllBytesPath = $"{hotfixAssembliesDstDir}/{dll}.bytes";
                File.Copy(dllPath, dllBytesPath, true);
                Debug.Log($"[CopyHotUpdateAssembliesToStreamingAssets] copy hotfix dll {dllPath} -> {dllBytesPath}");
            }
        }

        public static void CopyAssetBundlesToStreamingAssets(BuildTarget target)
        {
            string streamingAssetPathDst = Application.streamingAssetsPath;
            Directory.CreateDirectory(streamingAssetPathDst);
            string outputDir = GetAssetBundleOutputDirByTarget(target);
            var abs = new string[] { "prefabs" };
            foreach (var ab in abs)
            {
                string srcAb = ToRelativeAssetPath($"{outputDir}/{ab}");
                string dstAb = ToRelativeAssetPath($"{streamingAssetPathDst}/{ab}");
                Debug.Log($"[CopyAssetBundlesToStreamingAssets] copy assetbundle {srcAb} -> {dstAb}");
                AssetDatabase.CopyAsset( srcAb, dstAb);
            }
        }
}