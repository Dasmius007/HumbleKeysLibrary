using HumbleKeys.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Playnite.SDK.Data;

namespace HumbleKeys.Services
{
    public class HumbleKeysAccountClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IWebView webView;
        private const string loginUrl = @"https://www.humblebundle.com/login?goto=%2Fhome%2Flibrary&qs=hmb_source%3Dnavbar";
        private const string libraryUrl = @"https://www.humblebundle.com/home/library?hmb_source=navbar";
        private const string logoutUrl = @"https://www.humblebundle.com/logout?goto=/";
        private const string orderUrlMask = @"https://www.humblebundle.com/api/v1/order/{0}?all_tpkds=true";

        const string subscriptionCategory = @"subscriptioncontent";
        readonly bool preferCache;
        readonly string localCachePath;
        public HumbleKeysAccountClient(IWebView webView) { this.webView = webView; }
        
        public HumbleKeysAccountClient(IWebView webView, IHumbleKeysAccountClientSettings clientSettings) : this(webView)
        {
            localCachePath = Directory.Exists(clientSettings.CachePath) ? clientSettings.CachePath : new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName;

            preferCache = clientSettings.CacheEnabled;
            // initialise folder structure for local cache
            var cachePaths = new[] { "order", "membership" };
            if (preferCache)
            {
                foreach (var cachePath in cachePaths)
                {
                    if (!Directory.Exists($"{localCachePath}\\{cachePath}"))
                    {
                        Directory.CreateDirectory($"{localCachePath}\\{cachePath}");
                    }
                }
            }
            else
            {
                File.Delete($"{localCachePath}\\gameKeys.json");
                foreach (var cachePath in cachePaths)
                {
                    if (!Directory.Exists($"{localCachePath}\\{cachePath}")) continue;
                    
                    var cachedFiles = Directory.EnumerateFiles($"{localCachePath}\\{cachePath}");
                    foreach (var cachedFile in cachedFiles)
                    {
                        File.Delete(cachedFile);
                    }
                    Directory.Delete($"{localCachePath}\\{cachePath}");
                }
                logger.Info("Cache cleared");
            }
        }

        public void Login()
        {
            //webView.NavigationChanged += (s, e) =>
            webView.LoadingChanged += (s, e) =>
            {
                if (webView.GetCurrentAddress() == libraryUrl)
                {
                    webView.Close();
                }
            };

            webView.DeleteDomainCookies(".humblebundle.com");
            webView.DeleteDomainCookies("www.humblebundle.com");
            webView.Navigate(loginUrl);
            webView.OpenDialog();
        }


        public bool GetIsUserLoggedIn()
        {
            webView.NavigateAndWait(libraryUrl);
            return webView.GetPageSource().Contains("\"gamekeys\":");
        }


        internal List<string> GetLibraryKeys()
        {
            var keysCacheFilename = $"{localCachePath}\\gamekeys.json";
            if (preferCache)
            {
                // Request may be cached in local filesystem to prevent spamming Humble
                var cachedData = GetCacheContent<List<string>>(keysCacheFilename);
                if (cachedData != null)
                {
                    return cachedData;
                }
            }

            webView.NavigateAndWait(libraryUrl);
                var libSource = webView.GetPageSource();
                var match = Regex.Match(libSource, @"""gamekeys"":\s*(\[.+\])");
                if (!match.Success) throw new Exception("User is not authenticated.");
                
                var strKeys = match.Groups[1].Value;
                logger.Trace(
                    $"Request:{libraryUrl} Content:{Serialization.ToJson(Serialization.FromJson<List<string>>(strKeys), true)}");
                if (preferCache)
                {
                    CreateCacheContent(keysCacheFilename,strKeys);
                }
                return Serialization.FromJson<List<string>>(strKeys);

        }

        string GetCacheContent(string keysCacheFilename)
        {
            if (!File.Exists(keysCacheFilename)) return null;
            var streamReader = new StreamReader(new FileStream(keysCacheFilename, FileMode.Open));
            var cacheContent = streamReader.ReadToEnd();
            streamReader.Close();
            return cacheContent;
        }
        T GetCacheContent<T>(string keysCacheFilename) where T : class
        {
            var cacheContent = GetCacheContent(keysCacheFilename);
            return cacheContent == null ? null : Serialization.FromJson<T>(cacheContent);
        }

        void CreateCacheContent(string cacheFilename, string strCacheEntry)
        {
            var streamWriter = new StreamWriter(new FileStream(cacheFilename, FileMode.OpenOrCreate));
            streamWriter.Write(strCacheEntry);
            streamWriter.Close();
        }
        
        internal Dictionary<string, Order> GetOrders(List<string> gameKeys, bool includeChoiceMonths = false)
        {
            var orders = new Dictionary<string, Order>();
            foreach (var key in gameKeys)
            {
                var orderUri = string.Format(orderUrlMask, key);
                var cacheFileName = $"{localCachePath}/order/{key}.json";
                Order order = null;
                if (preferCache)
                {
                    order = GetCacheContent<Order>(cacheFileName);
                }
                if (order == null) {
                    webView.NavigateAndWait(orderUri);
                    var strContent = webView.GetPageText();
                    if (preferCache)
                    {
                        CreateCacheContent(cacheFileName,strContent);
                    }
                    order = Serialization.FromJson<Order>(strContent);
                }
                logger.Trace($"Request:{orderUri} Content:{Serialization.ToJson(order, true)}");

                if (string.Equals(order.product.category, subscriptionCategory, StringComparison.Ordinal) && !string.IsNullOrEmpty(order.product.choice_url) && includeChoiceMonths)
                {
                    AddChoiceMonthlyGames(order);
                }
                orders.Add(order.gamekey, order);
            }

            return orders;
        }

        void AddChoiceMonthlyGames(Order order)
        {
            var cachePath = $"membership/{order.product.choice_url}";
            var choiceUrl = $"https://www.humblebundle.com/{cachePath}";
            var strChoiceMonth = string.Empty;
            var orderCacheFilename = $"{localCachePath}/{cachePath}.json";
            if (preferCache)
            {
                // Request may be cached in local filesystem to prevent spamming Humble
                strChoiceMonth = GetCacheContent(orderCacheFilename);
            }

            if (string.IsNullOrEmpty(strChoiceMonth))
            {
                webView.NavigateAndWait(choiceUrl);
                var match = Regex.Match(webView.GetPageSource(),
                    @"<script id=""webpack-monthly-product-data"" type=""application/json"">([\s\S]*?)</script>");
                if (match.Success)
                {
                    strChoiceMonth = match.Groups[1].Value;
                    if (preferCache)
                    {
                        // save data into cache
                        CreateCacheContent(orderCacheFilename, strChoiceMonth);
                    }
                }
                else
                {
                    logger.Error($"Unable to obtain Choice Monthly data for entry [{order.product.choice_url}]");
                }
            }

            if (string.IsNullOrEmpty(strChoiceMonth)) return;
            
            IChoiceMonth choiceMonth = null;
            if (order.product.is_subs_v2_product)
            {
                choiceMonth = Serialization.FromJson<ChoiceMonthV2>(strChoiceMonth);
                logger.Trace($"Request:{choiceUrl} Content:{Serialization.ToJson(choiceMonth, true)}");
            }
            else if (order.product.is_subs_v3_product)
            {
                choiceMonth = Serialization.FromJson<ChoiceMonthV3>(strChoiceMonth);
                logger.Trace($"Request:{choiceUrl} Content:{Serialization.ToJson(choiceMonth, true)}");
            }
            else
            {
                logger.Error("Unknown Choice Monthly product version");
            }

            if (choiceMonth == null) return;
                
            foreach (var contentChoice in choiceMonth.ContentChoices)
            {
                if (contentChoice.tpkds != null)
                {
                    order.tpkd_dict.all_tpks.AddRange(contentChoice.tpkds);
                }
                else if (contentChoice.nested_choice_tpkds != null)
                {
                    foreach (var nestedChoiceTpkd in contentChoice.nested_choice_tpkds)
                    {
                        order.tpkd_dict.all_tpks.AddRange(nestedChoiceTpkd.Value);
                    }
                }
                else
                {
                    logger.Error($"Unable to retrieve tpkds for Choice Month Title:{choiceMonth.Title}");
                }
            }
        }

        internal List<Order> GetOrders(string cachePath)
        {
            var orders = new List<Order>();
            foreach (var cacheFile in Directory.GetFiles(cachePath))
            {
                orders.Add(Serialization.FromJsonFile<Order>(cacheFile));
            }

            return orders;
        }
    }
}
