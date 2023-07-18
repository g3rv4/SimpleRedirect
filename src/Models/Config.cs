using System.Linq;

namespace SimpleRedirect.Models
{
    public class Config
    {
        public string Key { get; set; }
        public DomainData[] Domains { get; set; }

        private string[] _validDomains;
        public string[] ValidDomains => _validDomains ??= Domains.Select(d => d.Domain).ToArray();

        public class DomainData
        {
            public string Domain { get; set; }
            public string DataUrl { get; set; }
            public CloudflareData Cloudflare { get; set; }

            public class CloudflareData
            {
                public string[] Zones { get; set; }
                public string ApiKey { get; set; }
            }
        }
    }
}