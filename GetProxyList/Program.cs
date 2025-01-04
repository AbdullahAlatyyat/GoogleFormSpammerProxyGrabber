using GetProxyList;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

class Program
{
    public List<Proxies> allProxiesList = new List<Proxies>();
    public ConcurrentBag<Proxies> toPushProxies = new ConcurrentBag<Proxies>();
    public int confMode;

    public ConnectionMultiplexer redis;

    public static async Task Main(string[] args)
    {
        Program p = new Program();

#if DEBUG
        p.redis = ConnectionMultiplexer.Connect(
            new ConfigurationOptions
            {
                EndPoints = { "100.74.130.10:6379" }
            });
#else
        p.redis = ConnectionMultiplexer.Connect(
            new ConfigurationOptions
            {
                EndPoints = { "127.0.0.1:6379" }
            });
#endif

        var configFile = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.JSON")));
        var url = configFile["url"];
        var data = configFile["data"];
        var waitingTime = Convert.ToInt32(configFile["waiting"]);

        p.confMode = 1;

        while (true)
        {
            await p.GetProxylist();
            List<Proxies> loopList = new List<Proxies>(p.allProxiesList);

            List<Task> tasks = new List<Task>();
            foreach (var proxy in loopList)
            {
                var currentProxy = new Proxies()
                {
                    IP = proxy.IP,
                    PORT = proxy.PORT
                };
                var proxyUrl = $"http://{proxy.IP}:{proxy.PORT}";

                Task t = Task.Factory.StartNew(() => p.SendRequestBackground(url, data, currentProxy, proxyUrl).GetAwaiter().GetResult());
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());
            p.PushToRedis();

            p.confMode++;

            if (p.confMode == 3)
                p.confMode = 1;

            Console.WriteLine($"Waiting {waitingTime} mins");
            Thread.Sleep(waitingTime * 60 * 1000);
        }
    }

    private void PushToRedis()
    {
        if (toPushProxies.Count > 10)
        {
            var db = redis.GetDatabase();
            var obj = JsonConvert.SerializeObject(toPushProxies);
            var result = db.StringSet($"proxies", obj);
            if (result)
                Console.WriteLine($"Redis Updated: {toPushProxies.Count}");
            else
                Console.WriteLine($"Failed Redis Update");
        }
    }

    private async Task SendRequestBackground(string confUrl, string confData, Proxies currentProxy, string proxyUrl)
    {
        HttpClientHandler handler = CreateHandler(proxyUrl);

        HttpResponseMessage response = null;
        HttpStatusCode responseCode = HttpStatusCode.Created;

        using (var httpClient = new HttpClient(handler, true))
        {
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), confUrl + "/formResponse"))
            {
                try
                {
                    PrepareRequest(confData, request);
                    response = await httpClient.SendAsync(request);
                    responseCode = response.StatusCode;
                }
                catch (Exception)
                {
                }
            }
        }

        if (responseCode == HttpStatusCode.OK)
        {
            var responseHTML = await response.Content.ReadAsStringAsync();
            if (responseHTML.Contains("Google Docs"))
            {
                Console.WriteLine($"Pushed IP {currentProxy.IP} PORT {currentProxy.PORT}");
                toPushProxies.Add(currentProxy);
            }
        }
    }

    private HttpClientHandler CreateHandler(string proxyUrl)
    {
        var proxy = new WebProxy
        {
            Address = new Uri(proxyUrl),
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
        };

        var handler = new HttpClientHandler
        {
            Proxy = proxy,
        };

        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        handler.AutomaticDecompression = ~DecompressionMethods.None;
        return handler;
    }

    private void PrepareRequest(string data, HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("authority", "docs.google.com");
        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("dnt", "1");
        request.Headers.TryAddWithoutValidation("origin", "https://yourmother.com");
        request.Headers.TryAddWithoutValidation("referer", "https://yourmother.com/");
        request.Headers.TryAddWithoutValidation("sec-ch-ua", "^^");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "^^");
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        request.Headers.TryAddWithoutValidation("sec-fetch-site", "cross-site");
        request.Headers.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36 Edg/109.0.1518.61");

        request.Content = new StringContent(data);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
    }

    private async Task GetProxylist()
    {
        allProxiesList = new List<Proxies>();
        toPushProxies = new ConcurrentBag<Proxies>();

        Console.WriteLine($"Started ProxyGrapper Config: {confMode}");

        switch (confMode)
        {
            case 1:
                await proxy1();
                break;
            case 2:
                await proxy2();
                break;
            case 3:
                await proxy3();
                break;
            default:
                break;
        }
    }

    private async Task proxy1()
    {
        var url = "https://www.proxy-list.download/api/v2/get?l=en&t=http";

        var handler = new HttpClientHandler();

        handler.AutomaticDecompression = ~DecompressionMethods.None;

        using (var httpClient = new HttpClient(handler))
        {
            using (var request = new HttpRequestMessage(new HttpMethod("GET"), url))
            {
                var response = await httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();
                var parsedResult = JsonConvert.DeserializeObject<ProxyModel1>(result);
                allProxiesList.AddRange(parsedResult.LISTA);
            }
        }
    }

    private async Task proxy2()
    {
        var url = "https://proxylist.geonode.com/api/proxy-list?limit=500&page=1&sort_by=lastChecked&sort_type=desc&protocols=http";

        var handler = new HttpClientHandler();

        handler.AutomaticDecompression = ~DecompressionMethods.None;

        using (var httpClient = new HttpClient(handler))
        {
            using (var request = new HttpRequestMessage(new HttpMethod("GET"), url))
            {
                var response = await httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();
                var parsedResult = JsonConvert.DeserializeObject<ProxyModel3>(result);
                allProxiesList.AddRange(parsedResult.data);
            }
        }
    }

    private async Task proxy3()
    {
        var url = "https://public.freeproxyapi.com/api/Download/Json";

        var handler = new HttpClientHandler();

        handler.AutomaticDecompression = ~DecompressionMethods.None;

        using (var httpClient = new HttpClient(handler))
        {
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), url))
            {
                request.Headers.TryAddWithoutValidation("authority", "public.freeproxyapi.com");
                request.Headers.TryAddWithoutValidation("accept", "application/octet-stream");
                request.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");
                request.Headers.TryAddWithoutValidation("dnt", "1");
                request.Headers.TryAddWithoutValidation("sec-ch-ua", "^^");
                request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
                request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "^^");
                request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
                request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
                request.Headers.TryAddWithoutValidation("origin", "https://yourmother.com");
                request.Headers.TryAddWithoutValidation("referer", "https://yourmother.com/");
                request.Headers.TryAddWithoutValidation("sec-fetch-site", "cross-site");
                request.Headers.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36 Edg/109.0.1518.61");

                request.Content = new StringContent("{\"types\":[3],\"levels\":[],\"countries\":[],\"type\":\"json\",\"resultModel\":\"Mini\"}");
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                var response = await httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();

                List<ProxyModel2> proxyList = JsonConvert.DeserializeObject<List<ProxyModel2>>(result);
                List<Proxies> proxies = new List<Proxies>();
                foreach (var item in proxyList)
                {
                    proxies.Add(new Proxies()
                    {
                        IP = item.Host,
                        PORT = item.Port.ToString()
                    });
                }

                allProxiesList.AddRange(proxies);
            }
        }
    }
}