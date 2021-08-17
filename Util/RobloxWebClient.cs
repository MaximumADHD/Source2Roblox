using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Win32;
using Newtonsoft.Json;

namespace Source2Roblox.Util
{
    public class RobloxWebClient : WebClient
    {
        private readonly string ROBLOX_COOKIES = "";

        public RobloxWebClient()
        {
            RegistryKey robloxCookies = Registry.CurrentUser.GetSubKey
            (
                "SOFTWARE", "Roblox",
                "RobloxStudioBrowser",
                "roblox.com"
            );

            foreach (string name in robloxCookies.GetValueNames())
            {
                string cookie = robloxCookies.GetString(name);
                Match match = Regex.Match(cookie, "COOK::<([^>]*)>");

                if (match.Groups.Count > 1)
                {
                    cookie = match.Groups[1].Value;

                    if (!string.IsNullOrEmpty(ROBLOX_COOKIES))
                        ROBLOX_COOKIES += "; ";

                    ROBLOX_COOKIES += $"{name}={cookie}";
                }
            }

            Headers.Set(HttpRequestHeader.Accept, "*/*");
            Headers.Set(HttpRequestHeader.Cookie, ROBLOX_COOKIES);
            Headers.Set(HttpRequestHeader.CacheControl, "no-cache");
            Headers.Set(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
        }

        public async Task<T> DownloadJson<T>(string url)
        {
            string json = await DownloadStringTaskAsync(url);
            return JsonConvert.DeserializeObject<T>(json);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address) as HttpWebRequest;
            request.Headers.Set(HttpRequestHeader.UserAgent, "RobloxStudio/WinInet");
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            return request;
        }
    }
}
