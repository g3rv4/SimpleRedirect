using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Jil;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SimpleRedirect.Models;

namespace SimpleRedirect.Controllers
{
    public class HomeController : Controller
    {
        private static IConfiguration _configuration;
        private static ConcurrentDictionary<string, Dictionary<string, string>> _domains =
            new ConcurrentDictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static Config _configMemoized;
        private static Config _config => _configMemoized ??= GetConfig();

        private static Config GetConfig()
        {
            var path = _configuration.GetValue<string>("CONFIG_PATH");
            using var file = System.IO.File.OpenRead(path);
            using var reader = new StreamReader(file);
            return JSON.Deserialize<Config>(reader);
        }
        
        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string _domain;
        private string Domain => _domain ??= Request.Host.Host.Replace("www.", "");

        private async Task<IActionResult> ProcessPathAsync(string path)
        {
            if (!_domains.TryGetValue(Domain, out var urls)) return null;

            if (urls.Count == 0)
            {
                // there's a load in progress... give it two seconds
                await Task.Delay(TimeSpan.FromSeconds(2));
                urls = _domains[Domain];
            }
            
            if (urls.TryGetValue(path, out var dest))
            {
                return Redirect(dest);
            }
            
            return Content("Page not found");
        }

        [Route("refresh")]
        public async Task<IActionResult> RefreshDomain(string domain, string key)
        {
            if (key != _config.Key)
            {
                return Content("Invalid key");
            }

            var domainConfig =
                _config.Domains.FirstOrDefault(d => d.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
            if (domainConfig == null)
            {
                return Content("Invalid domain");
            }

            _domains.TryRemove(domain, out _);
            var sb = new StringBuilder();
            if (domainConfig.Cloudflare != null)
            {
                foreach (var zone in domainConfig.Cloudflare.Zones)
                {
                    var request = new HttpRequestMessage
                    {
                        Content = new StringContent(@"{""purge_everything"":true}", Encoding.UTF8, "application/json"),
                        Method = HttpMethod.Delete,
                        RequestUri = new Uri($"https://api.cloudflare.com/client/v4/zones/{zone}/purge_cache")
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", domainConfig.Cloudflare.ApiKey);
                    var response = await _httpClient.SendAsync(request);
                    sb.AppendLine($"Clearing zone {zone}. Result: {response.StatusCode}");
                }
            }

            return Content(sb + "Ok");
        }

        [Route("{path?}", Order = int.MaxValue)]
        public async Task<IActionResult> Index(string path)
        {
            path ??= "";
            var res = await ProcessPathAsync(path);
            if (res != null)
            {
                return res;
            }
            
            // if we couldn't determine what to do is because the domain is not loaded. Now, that could happen
            // because it's invalid or because we haven't fetched it yet
            if (!_config.ValidDomains.Contains(Domain))
            {
                return Content("Invalid domain");
            }

            // alright, we know the domain is valid. Then we need to pull the data. But I don't want to do multiple
            // requests if a bunch of clients are hitting the same domain... so let's fake the domain being loaded
            _domains.TryAdd(Domain, new Dictionary<string, string>());

            await LoadDomainAsync(Domain);

            res = await ProcessPathAsync(path);
            return res ?? Content("Error loading the domain data");
        }

        private static HttpClient _httpClient = new HttpClient();
        private static async Task LoadDomainAsync(string domain)
        {
            var domainConfig = _config.Domains.FirstOrDefault(d => d.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
            if (domainConfig == null)
            {
                return;
            }

            var response = await _httpClient.GetAsync(domainConfig.DataUrl);
            var json = await response.Content.ReadAsStringAsync();
            var urls = JSON.Deserialize<Dictionary<string, string>>(json);
            _domains[domain] = new Dictionary<string, string>(urls, StringComparer.OrdinalIgnoreCase);
        }
    }
}
