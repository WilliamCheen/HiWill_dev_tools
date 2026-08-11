using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Main.VersionChecker;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.IO;
#endif

/// <summary>
/// 新版本下载器
/// </summary>
public class VersionNewDownloader : MonoBehaviour
{
    [HideInInspector] public string DownloadURL;

    [SerializeField] private Text downloadDesc;
    [SerializeField] private Button downloadBtn;
    [SerializeField] private Text percentText;
    [SerializeField] private RectTransform downloadingTrans;


    private void Awake()
    {
        DeleteOldPackages();
        downloadBtn.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(DownloadURL))
            {
#if UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
                Application.OpenURL(DownloadURL);
#else
                DownloadWindowsPackage(DownloadURL).Forget();
#endif
            }
            else
            {
                transform.parent.gameObject.SetActive(false);
                //Warning.Instance.WarningInfo("无下载资源", "");
            }
        });
    }

    private static (string, string) DefaultDownloadName(string url)
    {
        string name = Path.GetFileName(url);
        if (name.Contains("?")) name = name[..name.IndexOf('?')];
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        string path = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Downloads";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
#elif UNITY_ANDROID
        string path = $"{Application.persistentDataPath}";
#elif UNITY_IOS
        string path = $"{Application.persistentDataPath}";
#endif
        return (path, name);
    }

    /// <summary>
    /// 删除旧的安装包
    /// </summary>
    public static void DeleteOldPackages()
    {
        // zip 包直接下载到了 Downloads 下面，无需删除旧安装包
        return;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var (path, _) = DefaultDownloadName(null);
        if (!Directory.Exists(path)) return;

        try
        {
            string[] exeFiles = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly);
            int deletedCount = 0;
            foreach (var filePath in exeFiles)
            {
                File.Delete(filePath);
                deletedCount++;
                Debug.Log($"已删除旧安装包：{filePath}");
            }

            Debug.Log($"全部 {deletedCount} 个安装包已删除");
        }
        catch (Exception e)
        {
            Debug.LogError($"Delete old packages fail: {e.Message}");
        }
#endif
    }

    /// <summary>
    /// 下载 Windows 安装包
    /// </summary>
    /// <param name="url"></param>
    private async UniTaskVoid DownloadWindowsPackage(string url)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        SetShouldShowDownloadingContent(true);
        var (path, fileName) = DefaultDownloadName(url);

        IProgress<float> progress = Progress.CreateOnlyValueChanged<float>(p =>
        {
            if (percentText) percentText.text = $"{p:P0}";
        });
        var cToken = this.GetCancellationTokenOnDestroy();
        var resp = await Download(url, $"{path}/{fileName}", progress, cToken);
        if (resp != null && resp.IsSuccess && !string.IsNullOrEmpty(resp.data) && File.Exists(resp.data))
        {
            Application.OpenURL(path);
            Application.Quit();
            return;
        }

        SetShouldShowDownloadingContent(false);
        Debug.LogError(resp?.message ?? "下载更新失败");
        DeleteOldPackages();
#endif
    }

    /// <summary>
    /// 下载 Windows 安装包
    /// </summary>
    /// <param name="downloadUrl">下载地址</param>
    /// <param name="savePath">保存的路径</param>
    /// <param name="progressChange">下载进度</param>
    /// <param name="cancellationToken">取消 Token</param>
    /// <returns></returns>
    private static async UniTask<HttpHelper.Response<string>> Download(string downloadUrl, string savePath, IProgress<float> progressChange, CancellationToken cancellationToken)
    {
        using UnityWebRequest request = UnityWebRequest.Get(downloadUrl);
        request.downloadHandler = new DownloadHandlerFile(savePath) { removeFileOnAbort = true };

        HttpHelper.Response<string> respEntity;
        try
        {
            await request.SendWebRequest().ToUniTask(progress: progressChange, cancellationToken: cancellationToken);

            if (request.result == UnityWebRequest.Result.Success)
            {
                respEntity = new HttpHelper.Response<string>(0, "success", savePath);
            }
            else
            {
                respEntity = new HttpHelper.Response<string>(-7, "下载出现错误！", null);
            }
        }
        catch (OperationCanceledException cancelEx)
        {
            respEntity = new HttpHelper.Response<string>(-2, cancelEx.Message ?? "Request canceled!");
        }
        catch (Exception ex)
        {
            Debug.Log($"UniTask error: {ex.Message}");
            respEntity = new HttpHelper.Response<string>(-7, ex.Message ?? "下载出现错误！", null);
        }

        return respEntity;
    }

    /// <summary>
    /// 根据下载状态隐藏相关元素
    /// </summary>
    /// <param name="show"></param>
    private void SetShouldShowDownloadingContent(bool show)
    {
        if (downloadDesc) downloadDesc.gameObject.SetActive(!show);
        if (downloadingTrans) downloadingTrans.gameObject.SetActive(show);
        if (downloadBtn) downloadBtn.gameObject.SetActive(!show);
    }
}