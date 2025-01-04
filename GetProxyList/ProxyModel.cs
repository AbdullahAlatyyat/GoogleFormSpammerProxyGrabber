namespace GetProxyList
{
    public class ProxyModel1
    {
        public string UPDATED { get; set; }
        public string UPDATEDAV { get; set; }
        public string TOTAL { get; set; }
        public Proxies[] LISTA { get; set; }
    }


    public class ProxyModel3
    {
        public Proxies[] data { get; set; }
        public int total { get; set; }
        public int page { get; set; }
        public int limit { get; set; }
    }

    public class ProxyModel2
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }

    public class Proxies
    {
        public string IP { get; set; }
        public string PORT { get; set; }
    }
}
