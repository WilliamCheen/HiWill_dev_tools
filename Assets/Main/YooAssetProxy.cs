using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

public static class YooAssetProxy
{
    public class EngineState
    {
        public string Name;
        public string Error;
        public bool IsInDownloadProcess;
        public int DownloadCount;
        public long HasDownloadBytes;
        public long TotalDownloadBytes;
    }

    public static string CurrentPackageVersion;

    public static async UniTask<(bool isSuccess, string msg)> StartYooAsset(EPlayMode playMode, string hostServerIP, Action<EngineState> stateChanger, string packageName = "DefaultPackage")
    {
        EngineState engineState = new EngineState()
        {
            Name = "开始启动引擎"
        };
#if !UNITY_EDITOR
        if (playMode == EPlayMode.EditorSimulateMode)
        {
            Debug.LogError("不能用模拟器模式！");
            engineState.Error = "不能用模拟器模式！";
            stateChanger?.Invoke(engineState);
            return (false, "非编辑器模式下不能使用此模式！");
        }
#endif
        // YooAsset 启动
        var startResp = await StartYooAssetEngine(playMode, hostServerIP, hostServerIP, packageName);
        if (!startResp.Item1) return startResp;

        // 获取版本号
        engineState.Name = "获取资源版本号";
        stateChanger?.Invoke(engineState);
        CurrentPackageVersion = await UpdatePackageVersion(packageName);
        if (string.IsNullOrEmpty(CurrentPackageVersion))
        {
            engineState.Error = "获取资源版本错误！";
            stateChanger?.Invoke(engineState);
            return (false, engineState.Error);
        }

        // 更新资源清单
        engineState.Name = "更新资源清单";
        stateChanger?.Invoke(engineState);
        var maniRes = await UpdatePackageManifest(packageName);
        if (!maniRes.Item1)
        {
            engineState.Error = maniRes.Item2;
            stateChanger?.Invoke(engineState);
            return maniRes;
        }

        // 下载更新资源
        engineState.Name = "准备下载更新资源";
        stateChanger?.Invoke(engineState);
        var downloadResp = await DownloadAsync(packageName, engineState, stateChanger);
        return downloadResp;
    }

    private static async UniTask<(bool, string)> DownloadAsync(string packageName, EngineState state, Action<EngineState> stateChanger = null)
    {
        const int downloadingMaxNum = 10;
        const int failedTryAgain = 3;
        var package = YooAssets.GetPackage(packageName);
        var downloader = package.CreateResourceDownloader(new ResourceDownloaderOptions(downloadingMaxNum, failedTryAgain));

        // 没有需要下载的资源
        if (downloader.TotalDownloadCount == 0)
        {
            state.Name = "无资源更新";
            stateChanger?.Invoke(state);
            return (true, null);
        }

        // 需要下载的文件总数和总大小
        int totalDownloadCount = downloader.TotalDownloadCount;
        long totalDownloadBytes = downloader.TotalDownloadBytes;

        bool downloadSuccess = false;
        string downloadError = "下载错误";
        state.Name = "开始下载更新资源";
        state.IsInDownloadProcess = true;
        state.DownloadCount = totalDownloadCount;
        state.TotalDownloadBytes = totalDownloadBytes;
        stateChanger?.Invoke(state);

        // 当下载器结束（无论成功或失败）
        downloader.DownloadCompleted += data =>
        {
            state.Name = "下载完成";
            state.IsInDownloadProcess = false;
            stateChanger?.Invoke(state);
            downloadSuccess = data.Succeeded;
        };

        // 当下载器发生错误
        downloader.DownloadError += data =>
        {
            state.Error = data.ErrorInfo;
            downloadSuccess = false;
            downloadError = data.ErrorInfo;
            Debug.Log($"下载错误，文件名：{data.FileName}，信息：{data.ErrorInfo}");
        };

        // 当下载进度发生变化
        downloader.DownloadProgressChanged += data =>
        {
            state.HasDownloadBytes = data.CurrentDownloadBytes;
            stateChanger?.Invoke(state);
            Debug.Log($"文件总数：{data.TotalDownloadCount}，已下载文件数：{data.CurrentDownloadCount}，下载总大小：{data.TotalDownloadBytes}，已下载大小{data.CurrentDownloadBytes}");
        };

        // 当开始下载某个文件
        downloader.DownloadFileStarted += data => { Debug.Log($"开始下载，文件名：{data.FileName}，文件大小：{data.FileSize}"); };

        downloader.StartDownload();
        await downloader.ToUniTask();

        return (downloadSuccess, downloadError);
    }

    private static async UniTask<string> UpdatePackageVersion(string packageName)
    {
        var package = YooAssets.GetPackage(packageName);
        var operation = package.RequestPackageVersionAsync();
        await operation.ToUniTask();
        return operation.Status == EOperationStatus.Succeeded ? operation.PackageVersion :  operation.Error ?? "Unknow version";
    }

    private static async UniTask<(bool, string)> UpdatePackageManifest(string packageName)
    {
        var package = YooAssets.GetPackage(packageName);
        var operation = package.LoadPackageManifestAsync(new LoadPackageManifestOptions(CurrentPackageVersion, 120));
        await operation.ToUniTask();
        return (operation.Status == EOperationStatus.Succeeded, operation.Error);
    }

    private static async UniTask<(bool, string)> StartYooAssetEngine(EPlayMode playMode, string hostServerIP, string hostServerBackupIp, string packageName)
    {
        Debug.Log("初始化资源系统");

        // 初始化资源系统
        YooAssets.Initialize();

        // 创建默认的资源包
        var package = YooAssets.CreatePackage("DefaultPackage");
        if (package == null) return (false, "创建默认热更包错误！");

        // 设置该资源包为默认的资源包，可以使用YooAssets相关加载接口加载该资源包内容。
        //YooAssets.SetDefaultPackage(package);

        // 编辑器下的模拟模式
        if (playMode == EPlayMode.EditorSimulateMode)
        {
            var simulateBuildResult = EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualAssetBundle);
            var packageRoot = simulateBuildResult.PackageRootDirectory;
            var editorFileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
            var initParameters = new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = editorFileSystemParams
            };
            
            var operation = package.InitializePackageAsync(initParameters);
            await operation.ToUniTask();
            return (operation.Status == EOperationStatus.Succeeded, operation.Error);
        }
        else if (playMode == EPlayMode.HostPlayMode)
        {
            IRemoteService remoteServices = new RemoteServices(hostServerIP, hostServerBackupIp);
            var cacheFileSystemParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteServices);
            var builtinFileSystemParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();

            var initParameters = new HostPlayModeOptions();
            initParameters.BuiltinFileSystemParameters = builtinFileSystemParams;
#if UNITY_EDITOR
            initParameters.BuiltinFileSystemParameters = null;
#endif
            initParameters.CacheFileSystemParameters = cacheFileSystemParams;
            var operation = package.InitializePackageAsync(initParameters);
            await operation.ToUniTask();
            return (operation.Status == EOperationStatus.Succeeded, operation.Error);
        }
        else if (playMode == EPlayMode.OfflinePlayMode)
        {
            var buildinFileSystemParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            var initParameters = new OfflinePlayModeOptions();
            initParameters.BuiltinFileSystemParameters = buildinFileSystemParams;
            var operation = package.InitializePackageAsync(initParameters);
            await operation.ToUniTask();
            return (operation.Status == EOperationStatus.Succeeded, operation.Error);
        }

        return (false, "不支持此种启动模式");
    }

    private class RemoteServices : IRemoteService
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }

        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            return new[]
            {
                $"{_defaultHostServer}/{fileName}",
                $"{_fallbackHostServer}/{fileName}"
            };
        }
    }

    public static async UniTask<AssetHandle> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
    {
        var package = YooAssets.GetPackage("DefaultPackage");
        AssetHandle assetOperationHandle = package.LoadAssetAsync<T>("Assets/Res/" + path);
        await assetOperationHandle.ToUniTask();
        return assetOperationHandle;
    }

    public static async UniTask<GameObject> LoadGameObject(string fileName)
    {
        var assetOperationHandle = await LoadAssetAsync<GameObject>(fileName);
        await assetOperationHandle.ToUniTask();
        return assetOperationHandle.GetAssetObject<GameObject>();
    }

    public static async Task<AssetHandle> GetRawFileAsync2(string path)
    {
        var package = YooAssets.GetPackage("DefaultPackage");
        AssetHandle rawFileOperation = package.LoadAssetAsync<TextAsset>("Assets/Res/" + path);
        await rawFileOperation.ToUniTask();
        return rawFileOperation;
    }
}