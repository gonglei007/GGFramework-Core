using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GGFramework.Core
{
    /// <summary>
    /// 通用格式化/编码工具（纯函数、零第三方依赖、确定性可测）。
    /// 提炼自业务项目的网络工具类中与网络无关的通用部分。
    /// </summary>
    public static class FormatUtil
    {
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };

        /// <summary>
        /// 把字节数格式化为带单位的可读字符串，按 1024 进制逐级放大，最多两位小数。
        /// 例如 1024 → "1 KB"，1536 → "1.5 KB"，0 → "0 B"。
        /// </summary>
        public static string DataSize(double bytes)
        {
            double size = bytes;
            int order = 0;
            while (size >= 1024 && order < SizeUnits.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", size, SizeUnits[order]);
        }

        /// <summary>当前时间的 Unix 毫秒时间戳。</summary>
        public static long UnixTimestampMs()
        {
            return new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 计算字符串的 MD5，返回小写 32 位十六进制。UTF8 编码输入。
        /// 常用于接口签名（配合 <see cref="NetUtil.Sign"/>）。
        /// </summary>
        public static string Md5Hex(string text)
        {
            if (text == null)
            {
                text = string.Empty;
            }
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                byte[] hash = md5.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
