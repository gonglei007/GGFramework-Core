using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using UnityEngine;

namespace GGFramework.Core
{
    /// <summary>
    /// 通用网络工具（纯函数为主、零第三方依赖）。提炼自业务项目网络工具类中可复用的部分，
    /// 已剔除 MonoBehaviour 基类、SimpleJson 依赖及编辑器/业务专属方法（如 4G 判断、IP 打码）。
    /// </summary>
    public static class NetUtil
    {
        /// <summary>DNS 解析失败/超时时返回的占位地址。</summary>
        public const string UnknownAddress = "0.0.0.0";

        /// <summary>从完整 URL 中取出主机名（host）。例如 https://a.b.com:8080/x?y=1 → a.b.com。</summary>
        public static string HostFromUrl(string url)
        {
            Uri uri = new Uri(url);
            return uri.Host;
        }

        /// <summary>
        /// 带超时的同步 DNS 解析，返回首个地址字符串；超时或异常返回 <see cref="UnknownAddress"/> 并记录错误。
        /// 注意：这是阻塞调用（内部等待 <paramref name="timeoutMs"/>），不要在主线程对不可达主机频繁调用。
        /// </summary>
        public static string ResolveHostAddress(string host, int timeoutMs = 3000)
        {
            string ip = UnknownAddress;
            try
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                IAsyncResult result = Dns.BeginGetHostAddresses(host, res => mre.Set(), null);
                bool success = mre.WaitOne(timeoutMs);
                if (success)
                {
                    IPAddress[] addresses = Dns.EndGetHostAddresses(result);
                    if (addresses != null && addresses.Length > 0)
                    {
                        return addresses[0].ToString();
                    }
                }
                else
                {
                    Debug.LogError("DNS resolve timed out!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("{0}\n{1}\n{2}", e, host, ip));
            }
            return ip;
        }

        /// <summary>
        /// 对形如 <c>path?a=1&amp;b=2</c> 的带查询串 URL 做签名：解析查询参数，按 key 以
        /// <see cref="StringComparer.InvariantCulture"/> 排序，依次拼接 <c>value#</c>，末尾追加
        /// <paramref name="secretKey"/>，取其 MD5 小写十六进制（见 <see cref="FormatUtil.Md5Hex"/>）。
        /// 缺少 <c>?</c> 视为非法参数并抛出异常。参数顺序不影响结果（因已按 key 排序）。
        /// </summary>
        public static string Sign(string urlWithQuery, string secretKey)
        {
            string[] segments = urlWithQuery.Split('?');
            if (segments.Length < 2)
            {
                throw new ArgumentException("Illegal params!!! " + urlWithQuery, "urlWithQuery");
            }

            string[] pairs = segments[1].Split('&');
            Dictionary<string, string> paramDict = new Dictionary<string, string>();
            foreach (string pair in pairs)
            {
                string[] kv = pair.Split('=');
                if (kv.Length == 2)
                {
                    paramDict[kv[0]] = kv[1];
                }
            }

            string[] keys = new string[paramDict.Keys.Count];
            paramDict.Keys.CopyTo(keys, 0);
            Array.Sort(keys, StringComparer.InvariantCulture);

            string signText = string.Empty;
            for (int i = 0; i < keys.Length; i++)
            {
                signText += paramDict[keys[i]] + "#";
            }
            signText += secretKey;

            return FormatUtil.Md5Hex(signText);
        }
    }
}
