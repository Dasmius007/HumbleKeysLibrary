using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using HumbleKeys.Models;
using HumbleKeys.Services;

namespace HumbleKeys
{
    [LoadPlugin]
    public class HumbleKeysLibrary : LibraryPlugin
    {
        #region === Constants ================
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string dbImportMessageId = "humblekeyslibImportError";
        private const string humblePurchaseUrlMask = @"https://www.humblebundle.com/downloads?key={0}";
        const string steamGameUrlMask = @"https://store.steampowered.com/app/{0}";
        const string steamSearchUrlMask = @"https://store.steampowered.com/search/?term={0}";
        private const string REDEEMED_STR = "Key: Redeemed";
        private const string UNREDEEMED_STR = "Key: Unredeemed";
        private const string UNREDEEMABLE_STR = "Key: Unredeemable";
        private const string EXPIRABLE_STR = "Key: Expirable";
        private static readonly string[] PAST_TAGS = { REDEEMED_STR, UNREDEEMED_STR, UNREDEEMABLE_STR, EXPIRABLE_STR, "Redeemed", "Unredeemed", "Unredeemable", "Expirable"};
        private const string HUMBLE_KEYS_SRC_NAME = "Humble Keys";
        private const string HUMBLE_KEYS_PLATFORM_NAME = "Humble Key: ";
        #endregion

        #region === Accessors ================
        private Guid STEAMPLUGINID { get; } = Guid.Parse("cb91dfc9-b977-43bf-8e70-55f46e410fab");
        private HumbleKeysLibrarySettings Settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("62ac4052-e08a-4a1a-b70a-c2c0c3673bb9");
        public override string Name => "Humble Keys Library";

        // Implementing Client adds ability to open it via special menu in Playnite.
        public override LibraryClient Client { get; } = new HumbleKeysLibraryClient();
        #endregion

        public HumbleKeysLibrary(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties { CanShutdownClient = false, HasCustomizedGameImport = true };
            Settings = new HumbleKeysLibrarySettings(this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new HumbleKeysLibrarySettingsView();
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            var importedGames = new List<Game>();
            var removedGames = new List<Game>();
            Exception importError = null;

            if (!Settings.ConnectAccount) { return importedGames; }

            try
            {
                var orders = ScrapeOrders();
                var selectedTpkds = SelectTpkds(orders);
                ProcessOrders(orders, selectedTpkds, ref importedGames, ref removedGames);
            }
            catch (Exception e)
            {
                importError = e;
                logger.Error($"Humble Keys Library: error {e}");
            }

            if (importError != null)
            {
                logger.Error($"Humble Keys Library: importError {dbImportMessageId}");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    dbImportMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    System.Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(dbImportMessageId);
            }

            return importedGames;
        }

        public Dictionary<string, Order> ScrapeOrders()
        {
            Dictionary<string, Order> orders;
            using (var view = PlayniteApi.WebViews.CreateOffscreenView(
                       new WebViewSettings { JavaScriptEnabled = false }))
            {
                var api = new Services.HumbleKeysAccountClient(view,
                    new HumbleKeysAccountClientSettings
                    {
                        CacheEnabled = Settings.CacheEnabled,
                        CachePath = $"{PlayniteApi.Paths.ExtensionsDataPath}\\{Id}"
                    });
                var keys = api.GetLibraryKeys();
                orders = api.GetOrders(keys, Settings.ImportChoiceKeys);
            }

            return orders;
        }

        public IEnumerable<IGrouping<string, Order.TpkdDict.Tpk>> SelectTpkds(Dictionary<string,Order> orders)
        {
            return orders.Select(kv => kv.Value)
                .SelectMany(a => a.tpkd_dict?.all_tpks)
                .Where(t => t != null
                            && Settings.keyTypeWhitelist.Contains(t.key_type)
                            && !string.IsNullOrWhiteSpace(t.gamekey)
                ).GroupBy(tpk => tpk.gamekey);
        }

        /// <summary>
        /// Adds all keys from @tpkds
        /// an Order may be a single purchase, a bundle purchase or a monthly subscription
        /// </summary>
        /// <param name="orders"></param>
        /// <param name="tpkds"></param>
        /// <param name="importedGames">List of Games added from orders</param>
        /// <param name="removedGames">List of Games removed from orders due to settings</param>
        protected void ProcessOrders(Dictionary<string,Order> orders, IEnumerable<IGrouping<string, Order.TpkdDict.Tpk>> tpkds, ref List<Game> importedGames, ref List<Game> removedGames)
        {
            var redeemedTag = PlayniteApi.Database.Tags.Add(REDEEMED_STR);
            var unredeemedTag = PlayniteApi.Database.Tags.Add(UNREDEEMED_STR);
            var unredeemableTag = PlayniteApi.Database.Tags.Add(UNREDEEMABLE_STR);
            
            PlayniteApi.Database.BeginBufferUpdate();
            foreach (var tpkdGroup in tpkds)
            {
                var tpkdGroupEntries = tpkdGroup.AsEnumerable();
                Tag humbleChoiceTag = null;
                var groupEntries = tpkdGroupEntries.ToList();
                if (Settings.ImportChoiceKeys && Settings.CurrentTagMethodology != "none" && groupEntries.Count() > 1)
                {
                    var isHumbleMonthly = orders[tpkdGroup.Key].product.human_name.Contains("Humble Monthly");
                    if (Settings.CurrentTagMethodology == "all" || Settings.CurrentTagMethodology == "monthly" && isHumbleMonthly)
                    {
                        humbleChoiceTag = PlayniteApi.Database.Tags.Add($"Bundle: {orders[tpkdGroup.Key].product.human_name}");
                    }
                }

                var bundleContainsUnredeemableKeys = false;
                var sourceOrder = orders[tpkdGroup.Key];
                if (sourceOrder != null && sourceOrder.product.category != "storefront" && sourceOrder.total_choices > 0 && sourceOrder.product.is_subs_v2_product)
                {
                    bundleContainsUnredeemableKeys = sourceOrder.choices_remaining == 0;
                }

                // Monthly bundle has all choices made
                if (bundleContainsUnredeemableKeys && humbleChoiceTag != null)
                {
                    // search Playnite db for all games that are not included in groupEntries, these can be removed
                    var virtualOrders = groupEntries.Where(tpk => tpk.is_virtual).Select(GetGameId) ??
                                        new List<string>();
                    var gameKeys = virtualOrders.ToList();
                    // for this bundle, get all games from the database that are not in the keys collection for this order
                    var libraryKeysNotInOrder = PlayniteApi.Database.Games
                        .Where(game =>
                            game.TagIds != null && game.TagIds.Contains(humbleChoiceTag.Id) && gameKeys.Contains(game.GameId))
                        .ToList();
                    foreach (var game in libraryKeysNotInOrder)
                    {
                        switch (Settings.CurrentUnredeemableMethodology)
                        {
                            case "tag":
                            {
                                game.TagIds.Remove(unredeemedTag.Id);
                                if (game.TagIds.Contains(unredeemableTag.Id)) continue;

                                game.TagIds.Add(unredeemableTag.Id);
                                PlayniteApi.Notifications.Add(
                                    new NotificationMessage("HumbleKeysLibraryUpdate_"+game.Id,
                                        $"{game.Name} is no longer redeemable", NotificationType.Info,
                                        () =>
                                        {
                                            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
                                                return;
                                            PlayniteApi.MainView.SelectGame(game.Id);
                                        })
                                );
                                break;
                            }
                            case "delete":
                            {
                                if (PlayniteApi.Database.Games.Remove(game))
                                {
                                    removedGames.Add(game);
                                }

                                break;
                            }
                        }
                    }
                }

                var steamLibraryPlugin = PlayniteApi.Addons.Plugins.FirstOrDefault(plugin => plugin.Id == STEAMPLUGINID);
                
                foreach (var tpkd in groupEntries)
                {
                    var gameId = GetGameId(tpkd);
                    var gameEntry = PlayniteApi.Database.Games.FirstOrDefault(game => game.GameId == gameId && game.PluginId == Id);

                    var newGameEntry = false;
                    if (gameEntry == null)
                    {
                        if (!Settings.IgnoreRedeemedKeys || (Settings.IgnoreRedeemedKeys && !IsKeyPresent(tpkd)))
                        {
                            gameEntry = ImportNewGame(tpkd, humbleChoiceTag);
                            importedGames.Add(gameEntry);
                            newGameEntry = true;
                        }
                    }
                    
                    if (Settings.ExpirableNotification)
                    {
                        if (tpkd.num_days_until_expired > 0 && GetOrderRedemptionTagState(tpkd)==EXPIRABLE_STR)
                        {
                            PlayniteApi.Notifications.Add(
                                new NotificationMessage("HumbleKeysLibraryUpdate_expirable_" + gameEntry.Name,
                                    $"{gameEntry.Name}: Has an expiration date, it will expire in {tpkd.num_days_until_expired} days",
                                    NotificationType.Info,
                                    () =>
                                    {
                                        if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen) return;
                                        PlayniteApi.MainView.SelectGame(gameEntry.Id);
                                    })
                            );
                            var expiryNote = tpkd.expiration_date != DateTime.MinValue ? $"Key expires on: {tpkd.expiration_date.ToString(CultureInfo.CurrentCulture)}\n" : $"Key expires on: {DateTime.Now.AddDays(tpkd.num_days_until_expired).ToString(CultureInfo.CurrentCulture)}\n";
                            if (!gameEntry.Notes.Contains(expiryNote))
                            {
                                gameEntry.Notes += expiryNote;
                            }

                            PlayniteApi.Database.Games.Update(gameEntry);
                        }
                    }
                    
                    if (Settings.UnclaimedGameNotification)
                    {
                        // key present but no matching game in steam library
                        if (tpkd.steam_app_id != null)
                        {
                            var steamGame = PlayniteApi.Database.Games.FirstOrDefault(game =>
                                (game.GameId == tpkd.steam_app_id) && (steamLibraryPlugin != null &&
                                                                       game.PluginId == steamLibraryPlugin.Id));
                            if (steamGame == null && GetOrderRedemptionTagState(tpkd) == REDEEMED_STR)
                            {
                                PlayniteApi.Notifications.Add(
                                    new NotificationMessage("HumbleKeysLibraryUpdate_unclaimed_game_" + gameEntry.Id,
                                        $"{gameEntry.Name} does not exist in Steam library",
                                        NotificationType.Info,
                                        () =>
                                        {
                                            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
                                                return;
                                            PlayniteApi.MainView.SelectGame(gameEntry.Id);
                                        })
                                );
                            }
                        }
                    }

                    if (tpkd.sold_out && GetOrderRedemptionTagState(tpkd) != REDEEMED_STR)
                    {
                        PlayniteApi.Notifications.Add(
                            new NotificationMessage("HumbleKeysLibraryUpdate_sold_out_" + gameEntry.Name,
                                $"{gameEntry.Name} Key has sold out",
                                NotificationType.Info,
                                () =>
                                {
                                    if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen) return;
                                    PlayniteApi.MainView.SelectGame(gameEntry.Id);
                                })
                        );
                    }
                    if (newGameEntry)
                    {
                        continue;
                    }
                    
                    if (!Settings.IgnoreRedeemedKeys || (Settings.IgnoreRedeemedKeys && !IsKeyPresent(tpkd)))
                    {
                        var tagsUpdated = UpdateRedemptionStatus(gameEntry, tpkd, humbleChoiceTag);
                        var linksUpdated = UpdateStoreLinks(gameEntry.Links, tpkd);
                        if (!tagsUpdated && !linksUpdated) continue;

                        if (gameEntry.TagIds.Contains(unredeemableTag.Id))
                        {
                            switch (Settings.CurrentUnredeemableMethodology)
                            {
                                case "tag":
                                {
                                    PlayniteApi.Database.Games.Update(gameEntry);
                                    PlayniteApi.Notifications.Add(
                                        new NotificationMessage("HumbleKeysLibraryUpdate_"+gameEntry.Id,
                                            $"{gameEntry.Name} is no longer redeemable", NotificationType.Info,
                                            () =>
                                            {
                                                if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
                                                    return;
                                                PlayniteApi.MainView.SelectGame(gameEntry.Id);
                                            })
                                    );
                                    break;
                                }
                                case "delete":
                                {
                                    if (PlayniteApi.Database.Games.Remove(gameEntry))
                                    {
                                        removedGames.Add(gameEntry);
                                    }
                                    break;
                                }
                            }
                        }
                        else
                        {
                            PlayniteApi.Database.Games.Update(gameEntry);
                            PlayniteApi.Notifications.Add(
                                new NotificationMessage("HumbleKeysLibraryUpdate_"+gameEntry.Id,
                                    $"Tags Updated for {gameEntry.Name}: "+GetOrderRedemptionTagState(tpkd), NotificationType.Info,
                                    () =>
                                    {
                                        if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen) return;
                                        PlayniteApi.MainView.SelectGame(gameEntry.Id);
                                    })
                            );
                        }

                    }
                    else
                    {
                        // Remove Existing Game?
                        PlayniteApi.Database.Games.Remove(gameEntry);
                        logger.Trace(
                            $"Removing game {gameEntry.Name} since Settings.IgnoreRedeemedKeys is: [{Settings.IgnoreRedeemedKeys}] and IsKeyPresent() is [{IsKeyPresent(tpkd)}]");
                    }
                }
            }
            PlayniteApi.Database.EndBufferUpdate();
        }

        bool UpdateStoreLinks(ObservableCollection<Link> links, Order.TpkdDict.Tpk tpkd)
        {
            if (tpkd.key_type != "steam") return false;

            Link steamGameLink;
            string humanName = string.Empty;
            if (!string.IsNullOrEmpty(tpkd.steam_app_id))
            {
                steamGameLink = MakeSteamLink(tpkd.steam_app_id);
            }
            else
            {
                humanName = tpkd.human_name;
                steamGameLink = new Link("Steam", string.Format(steamSearchUrlMask, humanName.Replace(" ", "%2B")));
            }

            if (humanName.EndsWith(" Steam"))
            {
                humanName = humanName.Remove(humanName.LastIndexOf(" Steam", StringComparison.Ordinal));
            }
            if (humanName.EndsWith(" DLC"))
            {
                humanName = humanName.Remove(humanName.LastIndexOf(" DLC", StringComparison.Ordinal));
            }
            var steamLinks = links.Where((link1, i) => link1.Name == "Steam");
            var steamLinksList = steamLinks.ToList();
            var existingSteamLink = steamLinksList.FirstOrDefault();
            if (existingSteamLink == null)
            {
                links.Add(steamGameLink);
                return true;
            }

            if (!string.IsNullOrEmpty(tpkd.steam_app_id) && existingSteamLink.Url == steamGameLink.Url) return false;

            // steam link url doesn't match expected value
            if (existingSteamLink.Url != steamGameLink.Url)
            {
                existingSteamLink.Url = steamGameLink.Url;
            }
            else
            {
                return false;
            }

            return true;
        }

        Game ImportNewGame(Order.TpkdDict.Tpk tpkd, Tag groupTag = null)
        {
            var gameInfo = new GameMetadata()
            {
                Name = tpkd.human_name,
                GameId = GetGameId(tpkd),
                Source = new MetadataNameProperty(HUMBLE_KEYS_SRC_NAME),
                Platforms = new HashSet<MetadataProperty> { new MetadataNameProperty(
                        HUMBLE_KEYS_PLATFORM_NAME + tpkd.key_type) },
                Tags = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
            };
            
            // add tag reflecting redemption status
            gameInfo.Tags.Add(new MetadataNameProperty(GetOrderRedemptionTagState(tpkd)));
            
            if (!string.IsNullOrWhiteSpace(tpkd?.gamekey))
            {
                gameInfo.Links.Add(MakeLink(tpkd?.gamekey));
            }
            // adds link to humble purchase
            var links = new ObservableCollection<Link>();
            if (UpdateStoreLinks(links, tpkd))
            {
                gameInfo.Links.AddRange(links.ToList());
            }
            PlayniteApi.Database.BeginBufferUpdate();
            var game = PlayniteApi.Database.ImportGame(gameInfo, this);
            if (groupTag != null)
            {
                game.TagIds.Add(groupTag.Id);
                PlayniteApi.Database.Games.Update(game);
            }
            PlayniteApi.Database.EndBufferUpdate();
            return game;
        }

        // If a game is expired, add tag 'Key: Unredeemable'
        // If a game had been redeemed since last added to Playnite, remove the tag 'Key: Unredeemed' and add the tag 'Key: Redeemed'
        // returns whether tags were updated or not 
        bool UpdateRedemptionStatus(Game existingGame, Order.TpkdDict.Tpk tpkd, Tag groupTag = null)
        {
            var recordChanged = false;
            if (existingGame == null) { return false; }
            if (!Settings.keyTypeWhitelist.Contains(tpkd.key_type)) { return false; }

            // process tags on existingGame only if there was a change in tag status

            var existingRedemptionTagIds = existingGame.Tags?.Where(t => PAST_TAGS.Contains(t.Name)).ToList().Select(tag => tag.Id)??Enumerable.Empty<Guid>();
            
            // This creates a new Tag in the Tag Database if it doesn't already exist for 'Tag: Redeemed'
            var tagIds = existingRedemptionTagIds.ToList();
            var currentTagState = PlayniteApi.Database.Tags.Add(GetOrderRedemptionTagState(tpkd));

            if (groupTag != null)
            {
                if (existingGame.Tags != null && existingGame.Tags.All(tag => tag.Id != groupTag.Id))
                {
                    existingGame.TagIds.Add(groupTag.Id);
                    recordChanged = true;
                }
            }
            // existingGame already tagged with correct tag state
            if (tagIds.Contains(currentTagState.Id)) return recordChanged;

            // remove all tags related to key state
            existingGame.TagIds.RemoveAll(tagId => tagIds.Contains(tagId));

            existingGame.TagIds.Add(currentTagState.Id);

            return true;
        }

        #region === Helper Methods ============
        private static string GetGameId(Order.TpkdDict.Tpk tpk) => $"{tpk.machine_name}_{tpk.gamekey}";
        private static Link MakeLink(string gameKey) => new Link("Humble Purchase URL", string.Format(humblePurchaseUrlMask, gameKey) );
        private static Link MakeSteamLink(string gameKey) => new Link("Steam", string.Format(steamGameUrlMask, gameKey));
        private static bool IsKeyNull(Order.TpkdDict.Tpk t) => t?.redeemed_key_val == null;
        private static bool IsKeyPresent(Order.TpkdDict.Tpk t) => !IsKeyNull(t);
        private static string GetOrderRedemptionTagState(Order.TpkdDict.Tpk t)
        {
            if (t.is_expired) return UNREDEEMABLE_STR;
            if (t.num_days_until_expired > 0 && !IsKeyPresent(t)) return EXPIRABLE_STR;
            return IsKeyPresent(t) ? REDEEMED_STR : UNREDEEMED_STR;
        }

        #endregion
    }
}