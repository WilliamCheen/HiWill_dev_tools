using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AClockworkBerry;
using Main.Network;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Main.VersionChecker;
using YooAsset;
using HybridCLR;
using Main.UI;
using UniRx;

public class LoadDll : MonoBehaviour
{
    [SerializeField] private AppEnv env;
    [SerializeField] private EPlayMode playMode = EPlayMode.EditorSimulateMode;

    [SerializeField] private AppLaunchPage launchPage;
    [SerializeField] private Transform bootPackageTrans;
    [SerializeField] private VersionUpdatePanel versionUpdatePanel;

    // Start is called before the first frame update

    private readonly URLSchemeManager schemeMgr = new();
    private AppStartConfig appStartConfig;

    private void Awake()
    {
        AddScreenLoggerListener();

        // 检查启动参数
        schemeMgr.Start(OnActiveFromScheme).Forget();
        Debug.Log("LoadDll Awake");
    }

    private void OnActiveFromScheme(bool isValid, string msg, URLSchemeParameterEntity entity, bool isFirstCall)
    {
        if (isValid && entity != null)
        {
            if (isFirstCall)
            {
                appStartConfig = new AppStartConfig(entity);
                OnStart().Forget();
            }
            else
            {
                // 处理重新进入的情况
                OnReactive();
            }

            return;
        }

        Debug.Log($"From url scheme error: {msg}");
        appStartConfig = new AppStartConfig(env);

        OnStart().Forget();
    }

    private async UniTaskVoid OnStart()
    {
        // 开始处理流程，当前已经有了启动参数
        if (appStartConfig == null || AppStartConfig.Current == null)
        {
            Debug.LogError($"Start app config is invalid, app can't continue.");
            launchPage.UpdateContent("启动失败", "启动参数错误！");
            return;
        }

        var cToken = this.GetCancellationTokenOnDestroy();

        // 检查版本号
        launchPage.UpdateContent("检查版更新...", null);
        var resp = await AppVersionDataModel.CheckVersion(cToken);
        if (resp != null && resp.IsSuccess && resp.data != null && (resp.data.newVersionExist ?? false))
        {
            ShowNewVersionPop(resp.data);
            return;
        }

        // 无版本更新，继续下一流程：获取热更地址
        var hotResp = await AppVersionDataModel.GetHotFixUrl(AppStartConfig.MinIOPoint, cToken);
        if (resp == null || !resp.IsSuccess || string.IsNullOrEmpty(hotResp.data))
        {
            launchPage.UpdateContent("启动失败", "获取热更新地址错误！");
            return;
        }

        // 已获取热更地址，继续下一流程：启动 YooAsset
        var yooResp = await YooAssetProxy.StartYooAsset(playMode, hotResp.data, state =>
        {
            if (state == null) return;
            if (!state.IsInDownloadProcess) launchPage.UpdateContent(state.Name, state.Error);
            launchPage.UpdateProgress(state.IsInDownloadProcess, state.HasDownloadBytes, state.TotalDownloadBytes);
        });
        
        if (!yooResp.isSuccess)
        {
            Debug.Log($"YooAsset 启动失败");
            return;
        }

        // HybridCLR 加载元数据
        if (playMode != EPlayMode.EditorSimulateMode)
        {
            await LoadMetadataForAOTAssemblies();
        }

#if UNITY_EDITOR
        Assembly hotUpdateAss = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
#else
        AssetHandle raw = await YooAssetProxy.GetRawFileAsync2($"Code/Assembly-CSharp.dll.bytes");
        byte[] dllBytes = (raw.AssetObject as TextAsset).bytes;
        Assembly hotUpdateAss = Assembly.Load(dllBytes);
        Debug.Log("加载完毕热更");
#endif

        launchPage.UpdateContent("正在更新系统资源...", null);

        // 加载 Boot
        var boot = await YooAssetProxy.LoadGameObject("UIPrefabs/Boot");
        if (!boot)
        {
            launchPage.UpdateContent("加载 Boot 失败，请检查相关配置", null);
            return;
        }

        var go = Instantiate(boot);
        go.name = "Boot";
        go.transform.localPosition = Vector3.zero;
    }

    private static async UniTask LoadMetadataForAOTAssemblies()
    {
        List<string> aotDllList = new List<string>
        {
            "UniTask.dll",
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll", // 如果使用了Linq，需要这个
            "Newtonsoft.Json.dll",
            "UniRx.dll",
        };

        foreach (var aotDllName in aotDllList)
        {
            AssetHandle raw = await YooAssetProxy.GetRawFileAsync2($"Code/{aotDllName}.bytes");
            if (raw.AssetObject is TextAsset textAsset)
            {
                byte[] dllBytes = textAsset.bytes;
                LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
            }
        }
    }

    private void OnReactive()
    {
    }
    
    private void ShowNewVersionPop(AppVersionEntity entity)
    {
        if (versionUpdatePanel)
        {
            versionUpdatePanel.gameObject.SetActive(true);
            versionUpdatePanel.Config(entity);
        }
    }
    
    /// <summary>
    ///  显示日志输出
    /// </summary>
    private void AddScreenLoggerListener()
    {
        if (!ScreenLogger.Instance) return;
        const string localKey = "show_log_debug";
        if (PlayerPrefs.GetInt(localKey, 0) == 1)
        {
            ScreenLogger.Instance.ShowLog = true;
        }

        var clickStream = Observable.EveryUpdate()
            .Where(_ => Input.GetMouseButtonDown(0) && IsClickInHotZone(Input.mousePosition));
        var timeoutSingle = clickStream.Throttle(TimeSpan.FromSeconds(0.5f));
        clickStream
            .Buffer(timeoutSingle)
            .Where(clicks => ScreenLogger.Instance && clicks.Count >= 8)
            .Subscribe(_ =>
            {
                bool current = ScreenLogger.Instance.ShowLog;
                bool shouldShow = !current;
                ScreenLogger.Instance.ShowLog = shouldShow;
                PlayerPrefs.SetInt(localKey, shouldShow ? 1 : 0);
            })
            .AddTo(this);
    }

    private static bool IsClickInHotZone(Vector3 mousePosition)
    {
        bool isXInZone = mousePosition.x >= (Screen.width - 200);
        bool isYInZone = mousePosition.y <= 200;
        return isXInZone && isYInZone;
    }
}