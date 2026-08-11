using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;
using System.Security.Cryptography;

public static class HttpHelper
{
    [JsonObject(MemberSerialization.Fields)]
    public class Response<T>
    {
        public readonly int? code;
        public readonly string message;
        public readonly T data;

        public bool IsSuccess => code == 0;

        public Response(int? code, string message, T data)
        {
            this.code = code;
            this.message = message;
            this.data = data;
        }

        public Response(int? code, string message)
        {
            this.code = code;
            this.message = message;
        }
    }

    /// <summary>
    /// 以字典为参数的请求
    /// </summary>
    /// <typeparam name="T">解析类型</typeparam>
    /// <param name="api">接口 api，不包含 baseUrl </param>
    /// <param name="method">请求方法</param>
    /// <param name="parameters">请求参数</param>
    /// <param name="cToken">取消 source</param>
    /// <returns></returns>
    public static async UniTask<Response<T>> Request<T>(string api, string method, Dictionary<string, object> parameters = null, CancellationToken? cToken = null)
    {
        var (pathParams, serializedParams) = GenerateSerializedParameters(method, parameters);
        var response = await RequestPrimarily<T>(api, method, pathParams, serializedParams, cToken);
        return response;
    }

    /// <summary>
    /// 使用 url 参数字符串和参数排序并序列化后的 json 字符串请求
    /// </summary>
    /// <typeparam name="T">解析类型</typeparam>
    /// <param name="api">接口 api，不包含 baseUrl</param>
    /// <param name="method">请求方法</param>
    /// <param name="pathParameters">url 参数字符串，不作处理会直接加在 url 后面，如 ?name=Daniel/</param>
    /// <param name="serializedParameters">已排序可直接参与签名的参数 json 字符串</param>
    /// <param name="cToken">取消 source</param>
    /// <returns></returns>
    public static async UniTask<Response<T>> RequestPrimarily<T>(string api, string method, string pathParameters, string serializedParameters = null,
        CancellationToken? cToken = null)
    {
        string baseUrl = AppStartConfig.Current?.BaseUrl;
        string reqUrl = $"{baseUrl}{api}{pathParameters ?? ""}";
        Debug.Log($"Network: request url: {reqUrl}");

        using UnityWebRequest request = new(reqUrl, method);
        request.timeout = 90;
        SetupWebRequestHeader(request, serializedParameters, null);
        request.downloadHandler = new DownloadHandlerBuffer();
        if (!ShouldAppendParametersAtUrl(method) && serializedParameters != null)
        {
            Debug.Log($"Network: request body: {serializedParameters}");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(serializedParameters));
        }

        Response<T> respEntity = null;
        var cancel = cToken ?? new CancellationTokenSource().Token;
        try
        {
            await request.SendWebRequest().WithCancellation(cancel);
            respEntity = HandleResponse<T>(request);
        }
        catch (OperationCanceledException cancelEx)
        {
            Debug.Log($"request was canceled!");
            respEntity = new Response<T>(-2, cancelEx.Message ?? "Request canceled!");
        }
        catch (Exception ex)
        {
            Debug.Log($"UniTask error: {ex.Message}");
            respEntity = HandleResponse<T>(request);
        }

        return respEntity;
    }

    /// <summary>
    ///  GET/Head/Delete 请求 Url 参数拼接及参数序列化处理
    /// </summary>
    /// <param name="method"></param>
    /// <param name="originParameters"></param>
    /// <returns>（需要拼接到 Url 的参数字符串，参数排序并序列化成 json 字符串）</returns>
    public static (string pathParam, string serialzedParam) GenerateSerializedParameters(string method, Dictionary<string, object> originParameters, bool shouldSerialized = true)
    {
        if (originParameters == null)
        {
            return (null, JsonConvert.SerializeObject(new Dictionary<string, object>()));
        }

        var validParameters = new Dictionary<string, object>(originParameters);
        List<string> keysToRemove = new List<string>();
        foreach (var pair in validParameters)
        {
            if (pair.Value == null)
            {
                keysToRemove.Add(pair.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            validParameters.Remove(key);
        }

        bool shouldAppendAtUrl = ShouldAppendParametersAtUrl(method);
        var sortedDic = new SortedDictionary<string, object>(validParameters);

        string pathParams = "";
        if (shouldAppendAtUrl)
        {
            foreach (var pair in originParameters)
            {
                if (pair.Value is IList listP)
                {
                    List<string> values = new();
                    foreach (var item in listP)
                    {
                        if (item.GetType().IsPrimitive)
                        {
                            values.Add(item.ToString());
                        }
                        else if (item is string itemStr)
                        {
                            values.Add(itemStr);
                        }
                        else
                        {
                            values.Add(JsonConvert.SerializeObject(item));
                        }
                    }

                    sortedDic[pair.Key] = string.Join(", ", values);
                }
                else if (pair.Value.GetType().IsPrimitive)
                {
                    sortedDic[pair.Key] = pair.Value.ToString();
                }
                else if (pair.Value is string)
                {
                    sortedDic[pair.Key] = pair.Value;
                }
                else
                {
                    sortedDic[pair.Key] = JsonConvert.SerializeObject(pair.Value);
                }

                pathParams += $"{pair.Key}={sortedDic[pair.Key]}&";
            }

            if (pathParams.Length > 0)
            {
                pathParams = $"?{pathParams.Substring(0, pathParams.Length - 1)}";
            }
        }

        string serializedParam = shouldSerialized ? JsonConvert.SerializeObject(sortedDic) : null;
        return (pathParams, serializedParam);
    }

    /// <summary>
    /// 请求方法是否会将参数追加到 Url 上
    /// 根据 IECC
    /// </summary>
    /// <param name="method">请求方法</param>
    /// <returns></returns>
    private static bool ShouldAppendParametersAtUrl(string method)
    {
        return method == UnityWebRequest.kHttpVerbGET || method == UnityWebRequest.kHttpVerbHEAD || method == UnityWebRequest.kHttpVerbDELETE;
    }

    /// <summary>
    /// 请求结果处理
    /// </summary>
    /// <typeparam name="T">解析类型</typeparam>
    /// <param name="request">请求</param>
    /// <param name="config">请求更详细的配置</param>
    /// <returns></returns>
    private static Response<T> HandleResponse<T>(UnityWebRequest request)
    {
        string text = request.downloadHandler.text;
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var parseText = text.Replace(" ", "\u00A0");
                if (string.IsNullOrEmpty(text))
                {
                    return new Response<T>(-4, "Network response empty content.");
                }

                Debug.Log($"Network: response origin text: \n{text}");
                Response<T> entity = JsonConvert.DeserializeObject<Response<T>>(parseText);
                return entity;
            }
            catch (Exception ex)
            {
                return new Response<T>(-3, ex.Message);
            }
        }

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            return new Response<T>(-5, request.error ?? "Network connect error");
        }
        else if (request.result == UnityWebRequest.Result.ProtocolError)
        {
            //{"code":301,"message":"非法参数"}
            //{"status":404,"error":"Not Found","timestamp":1714382468367,"path":"/v1/cases"}
            int status = -1;
            string errorText = null;
            try
            {
                var protocolErrorDic = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                if (protocolErrorDic.TryGetValue("status", out object oStatus))
                {
                    int.TryParse(oStatus.ToString(), out status);
                }
                else if (protocolErrorDic.TryGetValue("code", out object oCode))
                {
                    int.TryParse(oCode.ToString(), out status);
                }

                if (protocolErrorDic.TryGetValue("error", out object oError))
                {
                    errorText = oError as string;
                }
                else if (protocolErrorDic.TryGetValue("message", out object oMessage))
                {
                    errorText = oMessage as string;
                }
            }
            catch
            {
                errorText = $"网络连接错误({request.responseCode})";
                Debug.Log($"Network Error: {request.error}");
            }

            return new Response<T>(status, errorText ?? "Http protocol error");
        }

        return new Response<T>(-1, "Http unknow error");
    }

    /// <summary>
    /// 设置请求头里的信息，包括 Token，签名，时间戳等
    /// </summary>
    /// <param name="request">请求</param>
    /// <param name="paramStr">排序好的参数 json 字符串</param>
    /// <param name="token"></param>
    private static void SetupWebRequestHeader(UnityWebRequest request, string paramStr, string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        string timestamp = Convert.ToInt64(ts.TotalSeconds).ToString();
        var shaStr = GetSha256Info($"{token ?? ""}{paramStr}{AppStartConfig.Current?.SignKey}");
        var sign = GetMD5Info($"{shaStr}{timestamp}");
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("medic-tutor-sign", sign);
        request.SetRequestHeader("medic-tutor-timestamp", timestamp);
        string platform = AppStartConfig.Current?.Platform ?? "TCM";
        request.SetRequestHeader("Medic-Tutor-Client", platform);
    }

    /// <summary>
    /// 检查网络连接性
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async UniTask<bool> NetworkReachable(CancellationToken token)
    {
        TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        string url = "/v1/captcha/random/" + Convert.ToInt64(ts.TotalSeconds);
        var resp = await Request<string>(url, "GET", null, cToken: token);
        return resp != null && resp.IsSuccess;
    }

    /// <summary>
    /// 等待网络可到达
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public static async UniTask WhenNetworkReachable(CancellationToken token)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            return;
        }

        bool notReachable = true;
        while (notReachable && !token.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: token);
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                bool canConnect = await NetworkReachable(token);
                notReachable = !canConnect;
            }
        }
    }

    private static string GetSha256Info(string content)
    {
        SHA256 sha256 = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hash = sha256.ComputeHash(bytes);
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < hash.Length; i++)
        {
            builder.Append(hash[i].ToString("x2"));
        }

        return builder.ToString();
    }

    private static string GetMD5Info(string content)
    {
        MD5 md5 = MD5.Create();

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hash = md5.ComputeHash(bytes);
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < hash.Length; i++)
        {
            builder.Append(hash[i].ToString("x2"));
        }

        return builder.ToString();
    }
}