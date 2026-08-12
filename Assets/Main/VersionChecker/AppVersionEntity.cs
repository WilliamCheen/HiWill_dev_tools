using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

namespace Main.VersionChecker
{
    [JsonObject(MemberSerialization.Fields)]
    public class AppVersionEntity
    {
        //下载地址
        public readonly string downloadUrl;

        //是否强制升级
        public bool? forcedUpgradeFlag;

        //最新版本号
        public readonly string newVersion;

        //是否有新版本 0：否 1：是
        public bool? newVersionExist;


#if UNITY_IOS
        public const string Platform = "iOS";
        public const string CurrentVersion = "1.0";
#elif UNITY_ANDROID
        public const string Platform = "android";
        public const string CurrentVersion = "1.0";
#elif UNITY_STANDALONE_WIN
        public const string Platform = "windows";
        public const string CurrentVersion = "1.0";
#elif UNITY_STANDALONE_OSX
        public const string Platform = "Mac";
        public const string CurrentVersion = "1.0";

#elif UNITY_STANDALONE_LINUX
        public const string Platform = "linux";
        public const string CurrentVersion = "1.0";

#else
        public const string Platform = "unknow";
        public const string CurrentVersion = "1.0";
#endif

#if UNITY_IOS || UNITY_ANDROID
        public const string API_VersionCheck = "/config/hiwill_dev_tool_version";
#else
        public const string API_VersionCheck = "/config/hiwill_dev_tool_version";
#endif
    }

    public static class AppVersionDataModel
    {
        /// <summary>
        /// 检查版本更新
        /// </summary>
        /// <param name="cToken"></param>
        /// <returns></returns>
        public static async UniTask<HttpHelper.Response<AppVersionEntity>> CheckVersion(CancellationToken cToken)
        {
            Dictionary<string, object> parameters = new()
            {
#if UNITY_IOS || UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
                { "platform", AppVersionEntity.Platform },
#endif
                { "version", AppVersionEntity.CurrentVersion }
            };

            await HttpHelper.WhenNetworkReachable(cToken);

            string api = $"{AppVersionEntity.API_VersionCheck}_{AppVersionEntity.Platform}.txt";
            var resp = await HttpHelper.Request<AppVersionEntity>(api, "GET", parameters, cToken);
            if (resp != null && resp.IsSuccess && resp.data != null)
            {
                var localVersion = new Version(AppVersionEntity.CurrentVersion);
                var remoteVersion = new Version(resp.data.newVersion);
                bool hasNewVersion = localVersion < remoteVersion;
                resp.data.newVersionExist = hasNewVersion;
                resp.data.forcedUpgradeFlag = true;
            }
            return resp;
        }

        public static async UniTask<HttpHelper.Response<string>> GetHotFixUrl(string baseUrl, CancellationToken cToken)
        {
            const string version = AppVersionEntity.CurrentVersion;
            const string platform = AppVersionEntity.Platform;
            if (AppStartConfig.Current?.Env == AppEnv.Dev || AppStartConfig.Current?.Env == AppEnv.Test)
            {
                string hotUpdateUrl = $"{baseUrl}/res-packages/{platform}/v{version}";
                return new HttpHelper.Response<string>(0, "success", hotUpdateUrl);
            }
            
            // 生产环境通过接口获取热更地址
            Dictionary<string, object> parameters = new() { { "platform", platform } };
            var resp = await HttpHelper.Request<string>("/v1/animation", "GET", parameters, cToken);
            return resp;
        }
    }
}