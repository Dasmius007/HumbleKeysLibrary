using HumbleKeys.Models;
using Playnite.SDK;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;


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

        const string SubscriptionCategory = @"subscriptioncontent";
        public HumbleKeysAccountClient(IWebView webView) { this.webView = webView; }


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
            webView.NavigateAndWait(libraryUrl);
            var libSource = webView.GetPageSource();
            var match = Regex.Match(libSource, @"""gamekeys"":\s*(\[.+\])");
            if (match.Success)
            {
                var strKeys = match.Groups[1].Value;
                return FromJson<List<string>>(strKeys);
            }
            else
            {
                throw new Exception("User is not authenticated.");
            }
        }


        internal List<Order> GetOrders(List<string> gamekeys, bool includeChoiceMonths = false)
        {
            var orders = new List<Order>();
            foreach (var key in gamekeys)
            {
                webView.NavigateAndWait(string.Format(orderUrlMask, key));
                var strContent = webView.GetPageText();
                var order = FromJson<Order>(strContent);
                if (string.Equals(order.product.category, @"subscriptioncontent", StringComparison.Ordinal) && !string.IsNullOrEmpty(order.product.choice_url) && includeChoiceMonths)
                {
                    AddChoiceMonthlyGames(order);
                }
                orders.Add(order);
            }

            return orders;
        }

        void AddChoiceMonthlyGames(Order order)
        {
            var choiceUrl = $"https://www.humblebundle.com/membership/{order.product.choice_url}";
            webView.NavigateAndWait(choiceUrl);
            var match = Regex.Match(webView.GetPageSource(), @"<script id=""webpack-monthly-product-data"" type=""application/json"">([\s\S]*?)</script>");
            if (match.Success)
            {
                IChoiceMonth choiceMonth = null;
                if (order.product.is_subs_v2_product)
                {
                    choiceMonth = FromJson<ChoiceMonthV2>(match.Groups[1].Value);
                }
                else if (order.product.is_subs_v3_product)
                {
                    choiceMonth = FromJson<ChoiceMonthV3>(match.Groups[1].Value);
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
                }
            }
            else
            {
                throw new Exception($"Unable to obtain Choice Monthly data for entry [{order.product.choice_url}]");
            }
        }


        internal List<Order> GetOrders(string cachePath)
        {
            var orders = new List<Order>();
            foreach (var cacheFile in Directory.GetFiles(cachePath))
            {
                orders.Add(FromJsonFile<Order>(cacheFile));
            }

            return orders;
        }


        #region === Helper Methods ================
        public static T FromJson<T>(string json) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                throw;
            }
        }

        public static T FromJsonFile<T>(string filePath) where T : class
        {
            return FromJson<T>(File.ReadAllText(filePath));
        }
        #endregion
    }
}
