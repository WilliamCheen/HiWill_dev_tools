using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN
using System.IO;
#endif

namespace Main.Network
{
    public class URLSchemeManager
    {
        private static bool _hasFirstCall = false;
        private static bool _isInJumpAction = false;
#if UNITY_STANDALONE_WIN
        private static FileSystemWatcher _watcher;
        private const string RestartFileName = "restart.txt";
#endif

        private Action<bool, string, URLSchemeParameterEntity, bool> onResponse;
        private bool hasStarted = false;

        public async UniTaskVoid Start(Action<bool, string, URLSchemeParameterEntity, bool> onReceive)
        {
            onResponse = onReceive;
            if (hasStarted) return;

            bool hasCall = false;
#if UNITY_STANDALONE_WIN
            hasCall = OnOpenedFromWindows();
            if (!hasCall)
            {
                hasCall = await TryParseSchemeFromLauncher();
            }
#else
            Application.deepLinkActivated += OnDeepLinkActive;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                OnDeepLinkActive(Application.absoluteURL);
                hasCall = true;
            }
#endif
            if (!hasCall) onReceive?.Invoke(false, null, null, true);
            hasStarted = true;
        }

        private void OnDeepLinkActive(string url)
        {
            var result = OnLinkActiveAction(url);
            var (isValid, msg, entity) = result;

            onResponse?.Invoke(isValid, msg, entity, !_hasFirstCall);
            _hasFirstCall = true;
        }

        /// <summary>
        /// 执行实际的跳转操作
        /// </summary>
        /// <param name="url"></param>
        private (bool isSuccess, string msg, URLSchemeParameterEntity) OnLinkActiveAction(string url)
        {
            if (_isInJumpAction)
            {
                return (false, "Busy, is in jump action now...", null);
            }

            if (string.IsNullOrEmpty(url))
            {
                //SetShowContent(false);
                return (false, "Invalid scheme url, please check!", null);
            }

            var entity = URLSchemeParameterEntity.ParseFromQuery(url);

            // 同一链接不执行后续操作
            if (entity != null && entity.SameDateWith(AppStartConfig.Current?.urlSchemeParams))
            {
                return (false, "Repeat url is invalid!", null);
            }

            if (entity == null || string.IsNullOrEmpty(entity.token))
            {
                return (false, "获取参数失败，请检查参数是否正确！", null);
            }

            return (true, null, entity);
        }

        #region WindowsPlatform

#if UNITY_STANDALONE_WIN
        private bool OnOpenedFromWindows()
        {
            string[] args = Environment.GetCommandLineArgs();
            return ParseWindowsArgs(args);
        }

        /// <summary>
        /// 尝试解析本地 StreamAssest 下的参数
        /// 解析成功后会删除源文件 Params.txt
        /// </summary>
        /// <returns></returns>
        private async UniTask<bool> TryParseSchemeFromLauncher()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "Params.txt");
            if (!File.Exists(filePath))
            {
                return false;
            }

            string paramText = null;
            using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (StreamReader reader = new(stream))
            {
                paramText = await reader.ReadToEndAsync().AsUniTask();
            }

            if (string.IsNullOrEmpty(paramText))
            {
                return false;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Debug.Log($"URLSchemePresenter: delete Param.txt file error: {ex.Message}");
            }

            string restartPath = Path.Combine(Application.streamingAssetsPath, RestartFileName);
            if (File.Exists(restartPath))
            {
                try
                {
                    File.Delete(restartPath);
                }
                catch (Exception ex)
                {
                    Debug.Log($"URLSchemePresenter: delete restart.txt file error: {ex.Message}");
                }
            }

            AddWindowsFileWatcher();
            return ParseWindowsArgs(new[] { paramText });
        }

        /// <summary>
        /// 添加 Windows 启动参数观察器
        /// </summary>
        private void AddWindowsFileWatcher()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath);
            _watcher = new FileSystemWatcher(filePath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            _watcher.Created += OnWindowsParamsChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnWindowsParamsChanged(object source, FileSystemEventArgs e)
        {
            if (e.Name == RestartFileName)
            {
                Application.Quit();
            }
        }

        /// <summary>
        /// 解析 Windows 参数
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private bool ParseWindowsArgs(string[] args)
        {
            if (args == null || args.Length == 0) return false;

            foreach (string param in args)
            {
                string value = Uri.UnescapeDataString(param);
                if (value.Contains("token="))
                {
                    OnDeepLinkActive(value);
                    return true;
                }
            }

            return false;
        }
#endif

        #endregion

        public void Destroy()
        {
            Application.deepLinkActivated -= OnDeepLinkActive;
#if UNITY_STANDALONE_WIN
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
#endif
        }
    }
}