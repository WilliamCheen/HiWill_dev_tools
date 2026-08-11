using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Main.VersionChecker;

public class VersionUpdatePanel : MonoBehaviour
{
    [SerializeField] private VersionNewDownloader versionDownloader;

    /// <summary>
    /// 检查当前版本是否需要升级
    /// </summary>
    public void Config(AppVersionEntity entity)
    {
        if (entity == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (entity.newVersionExist != null && entity.newVersionExist.Value)
        {
            if (entity.forcedUpgradeFlag == true)
            {
                versionDownloader.DownloadURL = entity.downloadUrl;
                versionDownloader.gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            gameObject.SetActive(false);
            // 如果无版本更新且本地存在旧的安装包，则删除该旧版本安装包
            VersionNewDownloader.DeleteOldPackages();
        }
    }
}