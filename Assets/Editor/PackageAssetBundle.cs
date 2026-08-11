using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 角色 AssetBundle 打包
/// </summary>
public class PackageAssetBundle : EditorWindow
{
    private string selectedFolderPath = "";
    private const string SelectedFolderPathKey = "UserSelectedFolderPath";

    private enum BuildTargetGroupEnum { Windows, Android, iOS, Mac, AllSupported }

    [MenuItem("Tools/角色打包")]
    public static void ShowSomething()
    {
        GetWindow<PackageAssetBundle>();
    }

    private void OnGUI()
    {
        string cachePath = EditorPrefs.GetString(SelectedFolderPathKey, "");
        if (!string.IsNullOrEmpty(cachePath) && Directory.Exists(cachePath))
        {
            selectedFolderPath = cachePath;
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Select save folder"))
        {
            SelectFolder();
        }

        EditorGUILayout.LabelField("Save at:", selectedFolderPath);

        GUILayout.Space(10);

        // 平台按钮，只有当选择了文件夹后才启用
        foreach (BuildTargetGroupEnum target in Enum.GetValues(typeof(BuildTargetGroupEnum)))
        {
            GUI.enabled = !string.IsNullOrEmpty(selectedFolderPath);
            if (GUILayout.Button(target.ToString()))
            {
                if (target == BuildTargetGroupEnum.AllSupported)
                {
                    _ = BuildAllPlatform();
                }
                else
                {
                    _ = BuildAssetBundle(target);
                }
            }
            GUI.enabled = true;
        }
    }

    private void SelectFolder()
    {
        string path = EditorUtility.OpenFolderPanel("Choose Directory to Save AssetBundles", "", "");
        if (!string.IsNullOrEmpty(path))
        {
            selectedFolderPath = path;
            EditorPrefs.SetString(SelectedFolderPathKey, selectedFolderPath);
            Repaint(); // 强制窗口刷新显示新的路径
        }
    }

    /// <summary>
    /// 根据不同的平台打包
    /// </summary>
    /// <param name="target"></param>
    private async Task BuildAssetBundle(BuildTargetGroupEnum target)
    {
        string abDataPath = Path.Combine(selectedFolderPath, target.ToString());
        if (!Directory.Exists(abDataPath))
        {
            Directory.CreateDirectory(abDataPath);
        }

        BuildTarget? bTarget = target switch
        {
            BuildTargetGroupEnum.Windows => BuildTarget.StandaloneWindows64,
            BuildTargetGroupEnum.Android => BuildTarget.Android,
            BuildTargetGroupEnum.iOS => BuildTarget.iOS,
            BuildTargetGroupEnum.Mac => BuildTarget.StandaloneOSX,
            _ => null,
        };

        if (bTarget == null)
        {
            return;
        }

        Debug.Log($"AssetBundle 打包将保存在：${abDataPath}");
        try
        {
            Debug.Log($"开始打包：{target} 平台！");
            var ab = BuildPipeline.BuildAssetBundles(abDataPath, BuildAssetBundleOptions.None, bTarget.Value);
            EditorUtility.ClearProgressBar();
            if (ab != null)
            {
                Debug.Log($"AssetBundle 打包成功！\n保存在：${abDataPath}");
            }
            else
            {
                Debug.LogError($"打包 {target} 平台失败!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"打包 {target} 平台失败，{ex.Message}");
        }
        
        await Task.Delay(500);
    }

    // 打包所有平台
    private async Task BuildAllPlatform()
    {
        await foreach (string result in GetAllPlatformEnumerable())
        {
            Debug.Log($"{result} 平台打包完毕！");
        }
    }

    private async IAsyncEnumerable<string> GetAllPlatformEnumerable()
    {
        var targets = Enum.GetValues(typeof(BuildTargetGroupEnum)).Cast<BuildTargetGroupEnum>();
        foreach (BuildTargetGroupEnum target in targets.Where(t => t != BuildTargetGroupEnum.AllSupported))
        {
            await BuildAssetBundle(target);
            yield return target.ToString();
        }
    }
}

