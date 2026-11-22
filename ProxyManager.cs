using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NetPulseAndroid
{
    public class ProxyItem
    {
        public string Ip { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }
        public string Country { get; set; }
        public int Latency { get; set; }

        public override string ToString() => $"{Ip}:{Port} [{Country}] {Latency}ms";
    }

    public static class ProxyManager
    {
        private static readonly HttpClient Client = new HttpClient();

        // Загружает лучшие прокси с ProxyScrape API v4
        public static async Task<List<ProxyItem>> FetchBestProxiesAsync(
            string countryCode = null, string protocol = "socks5", int maxLatency = 500)
        {
            var url = "https://api.proxyscrape.com/v4/free-proxy-list/get" +
                      "?request=display_proxies&proxy_format=ipport&format=json";

            if (!string.IsNullOrEmpty(countryCode))
                url += $"&country={countryCode}";
            if (!string.IsNullOrEmpty(protocol))
                url += $"&protocol={protocol}";
            if (maxLatency > 0)
                url += $"&timeout={maxLatency}";

            try
            {
                var response = await Client.GetStringAsync(url);
                var json = JObject.Parse(response);
                var list = new List<ProxyItem>();

                foreach (var token in json["proxies"] ?? json["data"] ?? new JArray())
                {
                    var item = new ProxyItem
                    {
                        Ip = token["ip"]?.ToString(),
                        Port = token["port"]?.ToObject<int>() ?? 0,
                        Protocol = token["protocol"]?.ToString(),
                        Country = token["country"]?.ToString(),
                        Latency = token["latency"]?.ToObject<int>() ?? 0
                    };
                    if (!string.IsNullOrEmpty(item.Ip) && item.Port > 0)
                        list.Add(item);
                }

                // Сортируем по возрастанию задержки — лучшие сверху
                list.Sort((a, b) => a.Latency.CompareTo(b.Latency));
                return list;
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("ProxyManager", ex.ToString());
                return new List<ProxyItem>();
            }
        }
    }
}
