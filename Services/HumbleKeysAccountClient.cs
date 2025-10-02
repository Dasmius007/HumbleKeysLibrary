using HumbleKeys.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var cachePaths = new[] { "order", "membership/v2","membership/v3","membership" };
            if (preferCache)
            {
                foreach (var cachePath in cachePaths)
                {
                    if (!Directory.Exists($"{localCachePath}\\{cachePath}"))
                    {
                        Directory.CreateDirectory($"{localCachePath}\\{cachePath}");
                    }
                }
                logger.Info("Cache directories prepared");
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

            logger.Trace("Fetching library keys from Humble Bundle");
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
            logger.Trace($"GetOrders: Processing {gameKeys.Count} game keys");

            foreach (var key in gameKeys)
            {
                var orderUri = string.Format(orderUrlMask, key);
                var cacheFileName = $"{localCachePath}/order/{key}.json";
                Order order = null;
                bool cacheHit;
                if (preferCache)
                {
                    order = GetCacheContent<Order>(cacheFileName);
                }

                if (order == null) {
                    cacheHit = false;
                    logger.Trace($"Fetching order details");
                    webView.NavigateAndWait(orderUri);
                    var strContent = webView.GetPageText();
                    if (preferCache)
                    {
                        CreateCacheContent(cacheFileName,strContent);
                    }
                    order = Serialization.FromJson<Order>(strContent);
                }
                else
                {
                    cacheHit = true;
                }
                logger.Trace($"Request:{orderUri} {(cacheHit?"Cached ":"")}Content:{Serialization.ToJson(order, true)}");

                if (string.Equals(order.product.category, subscriptionCategory, StringComparison.Ordinal) && !string.IsNullOrEmpty(order.product.choice_url) && includeChoiceMonths)
                {
                    AddChoiceMonthlyGames(order);
                }
                orders.Add(order.gamekey, order);
                logger.Trace($"GetOrders: Added order {order.gamekey} with {order.tpkd_dict.all_tpks.Count} total tpks");
            }

            logger.Trace($"GetOrders: Completed processing {orders.Count} orders");
            return orders;
        }

        void AddChoiceMonthlyGames(Order order)
        {
            string versionCachePath;
            if (order.product.is_subs_v2_product)
            {
                versionCachePath = "v2";
            } else if (order.product.is_subs_v3_product)
            {
                versionCachePath = "v3";
            }
            else
            {
                versionCachePath = "unknown";
            }

            var cachePath = $"membership/{versionCachePath}/{order.product.choice_url}";
            // if the monthly choice_url can be parsed, store the cache file in a ISO YYYY-MM file instead
            if (DateTime.TryParse(order.product.choice_url, out var choiceDate))
            {
                cachePath = $"membership/{versionCachePath}/{choiceDate:yyyy-MM}";
            }
            var choiceUrl = $"https://www.humblebundle.com/membership/{order.product.choice_url}";
            var strChoiceMonth = string.Empty;
            
            var orderCacheFilename = $"{localCachePath}/{cachePath}.json";
            var cacheHit = false;
            if (preferCache)
            {
                // Request may be cached in local filesystem to prevent spamming Humble
                strChoiceMonth = GetCacheContent(orderCacheFilename);
                cacheHit = !string.IsNullOrEmpty(strChoiceMonth);
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
                logger.Trace($"Request:{choiceUrl} {(cacheHit?"From Cache ":"")}Content:{Serialization.ToJson(choiceMonth, true)}");
            }
            else if (order.product.is_subs_v3_product)
            {
                choiceMonth = Serialization.FromJson<ChoiceMonthV3>(strChoiceMonth);
                logger.Trace($"Request:{choiceUrl} {(cacheHit?"From Cache ":"")}Content:{Serialization.ToJson(choiceMonth, true)}");
            }
            else
            {
                logger.Error("Unknown Choice Monthly product version");
            }

            if (choiceMonth == null) return;
            
            // Add contentChoice to all_tpks if it doesn't already exist (all_tpks gets populated by the order if it is already redeemed)
            // Only add to the order if the month contains redeemable games, may already have exhausted the selection count
            var orderMachineNames = order.tpkd_dict.all_tpks.Select(tpk => tpk.machine_name).ToList();

            var contentChoicesNotInOrder = choiceMonth.ContentChoices.Keys.ToList().Where(contentChoiceKey => !choiceMonth.ChoicesMade.Contains(contentChoiceKey));
            foreach (var contentChoiceKey in contentChoicesNotInOrder)
            {
                // get tkpds either directly or via nested_choice_tpkds
                Order.TpkdDict.Tpk[] orderEntries = null; 
                var contentChoice = choiceMonth.ContentChoices[contentChoiceKey];
                if (contentChoice.tpkds != null)
                {
                    orderEntries = contentChoice.tpkds;
                }
                else if (contentChoice.nested_choice_tpkds != null)
                {
                    var nestedOrderEntries = new List<Order.TpkdDict.Tpk>();
                    foreach (var nestedChoiceTpkd in contentChoice.nested_choice_tpkds)
                    {
                        nestedOrderEntries.AddRange(nestedChoiceTpkd.Value);
                    }
                    orderEntries = nestedOrderEntries.ToArray();
                }
                else
                {
                    logger.Error($"Unable to retrieve tpkds for Choice Month Title:{choiceMonth.Title}");
                }

                if (orderEntries == null) continue;
                
                foreach (var contentChoiceTpkd in orderEntries)
                {
                    contentChoiceTpkd.is_virtual = true;
                }

                order.tpkd_dict.all_tpks.AddRange(orderEntries);
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
