using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Main.Network
{
    /// <summary>
    /// 启动参数 Entity
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    public class URLSchemeParameterEntity
    {
        public readonly string token;
        public readonly string signKey;
        public readonly string contentId;
        public readonly string contentTestId;
        public readonly string studentContentId;
        public readonly string studentContentTestId;
        public readonly string arrangementStudentId;
        public readonly int? type;
        public readonly string fromScheme;
        public readonly string platform;
        public readonly string caseId;
        public readonly string fromName;
        public readonly string env;

        // 一个时间戳标记，用于分辨是否是同一跳转 URLScheme
        public readonly string date;

        [JsonIgnore] public bool JustForCaseTraining => type == 2 || type == 3;

        /// <summary>
        /// 返回主 App 时携带的参数
        /// </summary>
        /// <value></value>
        [JsonIgnore]
        public string BackFullSchemeParams
        {
            get
            {
                if (string.IsNullOrEmpty(fromScheme) || type == null || type == 1)
                {
                    return null;
                }

                bool hasArrId = !string.IsNullOrEmpty(arrangementStudentId);
                if (type == 2 && hasArrId && !string.IsNullOrEmpty(contentId))
                {
                    string full = $"{fromScheme}://?arrangementStudentId={arrangementStudentId}&contentId={contentId}";
                    if (!string.IsNullOrEmpty(studentContentId))
                    {
                        full += $"&studentContentId={studentContentId}";
                    }

                    return full;
                }

                if (type == 3 && hasArrId && !string.IsNullOrEmpty(contentTestId))
                {
                    string full = $"{fromScheme}://?arrangementStudentId={arrangementStudentId}&contentTestId={contentTestId}";
                    if (!string.IsNullOrEmpty(studentContentTestId))
                    {
                        full += $"$studentContentTestId={studentContentTestId}";
                    }

                    return full;
                }

                return null;
            }
        }

        // [JsonIgnore]

        /// <summary>
        /// 获取训练参数
        /// </summary>
        /// <value></value>
        [JsonIgnore]
        public Dictionary<string, object> TrainingParams
        {
            get
            {
                if (type == null || type == 1 || string.IsNullOrEmpty(caseId))
                {
                    return null;
                }

                if ((type == 2 || type == 3) && string.IsNullOrEmpty(arrangementStudentId))
                {
                    return null;
                }

                if (type == 2 && !string.IsNullOrEmpty(contentId))
                {
                    Dictionary<string, object> dic = new()
                    {
                        { "type", type.ToString() },
                        { "contentId", contentId },
                        { "arrangementStudentId", arrangementStudentId }
                    };
                    if (!string.IsNullOrEmpty(studentContentId))
                    {
                        dic.Add("studentContentId", studentContentId);
                    }

                    return dic;
                }

                if (type == 3 && !string.IsNullOrEmpty(contentTestId))
                {
                    Dictionary<string, object> dic = new()
                    {
                        { "type", type.ToString() },
                        { "contentTestId", contentTestId },
                        { "arrangementStudentId", arrangementStudentId }
                    };
                    if (!string.IsNullOrEmpty(studentContentTestId))
                    {
                        dic.Add("studentContentTestId", studentContentTestId);
                    }

                    return dic;
                }

                return null;
            }
        }

        /// <summary>
        /// 是否是同一 URLScheme，根据里面的 date 时间戳判断
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool SameDateWith(URLSchemeParameterEntity other)
        {
            if (other == null || string.IsNullOrEmpty(other.date) || string.IsNullOrEmpty(date))
            {
                return false;
            }

            return date == other.date;
        }

        /// <summary>
        /// 从 URL 参数里生成对象
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static URLSchemeParameterEntity ParseFromQuery(string url)
        {
            if (url == null || !url.Contains("?"))
            {
                return null;
            }

            Dictionary<string, object> map = new();
            string urlParams = url.Split("?").Last();
            string[] pair = urlParams.Split("&");
            for (int i = 0; i < pair.Length; i++)
            {
                string keyValue = pair[i];
                if (!keyValue.Contains("="))
                {
                    continue;
                }

                string[] keyAndValue = keyValue.Split("=");
                if (keyAndValue.Length != 2)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(keyAndValue[1]) && keyAndValue[1] != "null")
                {
                    map[keyAndValue[0]] = Uri.UnescapeDataString(keyAndValue[1]);
                }
            }

            try
            {
                var obj = JObject.FromObject(map).ToObject<URLSchemeParameterEntity>();
                if (string.IsNullOrEmpty(obj.token) || string.IsNullOrEmpty(obj.signKey) || string.IsNullOrEmpty(obj.platform))
                {
                    return null;
                }

                return obj;
            }
            catch (Exception ex)
            {
                Debug.Log($"JsonConvert: deserialize original json text error: {ex.Message}");
            }

            return null;
        }
    }
}