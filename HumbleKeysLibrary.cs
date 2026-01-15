using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
        private const string steamGameUrlMask = @"https://store.steampowered.com/app/{0}";
        private const string steamSearchUrlMask = @"https://store.steampowered.com/search/?term={0}";
        private const string REDEEMED_STR = "Key: Redeemed";
        private const string UNREDEEMED_STR = "Key: Unredeemed";
        private const string UNREDEEMABLE_STR = "Key: Unredeemable";
        private static readonly string[] PAST_TAGS = { REDEEMED_STR, UNREDEEMED_STR, UNREDEEMABLE_STR, "Redeemed", "Unredeemed", "Unredeemable"};
        private const string HUMBLE_KEYS_SRC_NAME = "Humble Keys";
        private const string HUMBLE_KEYS_PLATFORM_NAME = "Humble Key: ";
        private const string NINTENDO_SWITCH = "nintendo_switch";
        private const string PC_WINDOWS = "pc_windows";
        #endregion

        #region === Variables ================
        private Platform winPlatform;
        private Platform switchPlatform;
        private readonly KeyInfo humbleKeysSource = new KeyInfo { Name = "Unknown" };
        public override string Name => "Humble Keys";
        private SidebarItem importProgress;
        private HumbleKeysAccountClient humbleApi;

        private HumbleKeysAccountClient HumbleApi
        {
            get
            {
                if (humbleApi != null) return humbleApi;
                
                var view = PlayniteApi.WebViews.CreateOffscreenView(new WebViewSettings { JavaScriptEnabled = false });
                humbleApi = new HumbleKeysAccountClient(view,
                    new HumbleKeysAccountClientSettings
                    {
                        CacheEnabled = Settings.CacheEnabled,
                        CachePath = $"{PlayniteApi.Paths.ExtensionsDataPath}\\{Id}"
                    });
                return humbleApi;
            }
        }

        #endregion

        #region === Accessors ================
        private HumbleKeysLibrarySettings Settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("62ac4052-e08a-4a1a-b70a-c2c0c3673bb9");

        // Implementing Client adds ability to open it via special menu in Playnite.
        public override LibraryClient Client { get; } = new HumbleKeysLibraryClient();
        #endregion

        public HumbleKeysLibrary(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties { CanShutdownClient = false, HasCustomizedGameImport = true };
            Settings = new HumbleKeysLibrarySettings(this);
            Settings.PropertyChanged += OnSettingsOnPropertyChanged;
        }

        private void OnSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            humbleApi.Dispose();
            humbleApi = null;
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
                var orderKeys = HumbleApi.GetLibraryKeys();
                var orders = ScrapeOrders(orderKeys, args.CancelToken);
                //var orderDictionary = orders.ToDictionary(order => order.gamekey, order => order);
                //var selectedTpkds = SelectTpkds(orderDictionary);
                //logger.Trace("ImportGames: Selected Tpkds Count = " + selectedTpkds.Count());
                ProcessOrders(orders, orderKeys, ref importedGames, ref removedGames);
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

            logger.Trace($"ImportGames: Imported {importedGames.Count} games, Removed {removedGames.Count} games");
            humbleApi.Dispose();
            humbleApi = null;
            return importedGames;
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            importProgress = new SidebarItem
            {
                ProgressMaximum = 100f,
                ProgressValue = 0f,
                Type = SiderbarItemType.Button,
                Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "icon.png"),
                Title = "Humble Remote Progress",
                Visible = false
            };
            yield return importProgress;
        }

        private IEnumerable<Order> ScrapeOrders(List<string> orderKeys, CancellationToken cancellationToken = default)
        {
            logger.Trace($"ScrapeOrders: Keys Count = {orderKeys.Count}" );
            return HumbleApi.GetOrders(orderKeys, Settings.ImportChoiceKeys, cancellationToken);
        }

        /// <summary>
        /// Adds all keys from @tpkds
        /// an Order may be a single purchase, a bundle purchase or a monthly subscription
        /// </summary>
        /// <param name="orders"></param>
        /// <param name="orderKeys"></param>
        /// <param name="importedGames">List of Games added from orders</param>
        /// <param name="removedGames">List of Games removed from orders due to settings</param>
        private void ProcessOrders(IEnumerable<Order> orders, List<string> orderKeys, ref List<Game> importedGames, ref List<Game> removedGames)
        {
            var redeemedTag = PlayniteApi.Database.Tags.Add(REDEEMED_STR);
            var unredeemedTag = PlayniteApi.Database.Tags.Add(UNREDEEMED_STR);
            var unredeemableTag = PlayniteApi.Database.Tags.Add(UNREDEEMABLE_STR);

            var tagMethod = (TagMethodology)Settings.TagWithBundleName;
            var unredeemableMethod = (UnredeemableMethodology)Settings.UnredeemableKeyHandling;

            if (winPlatform == null) winPlatform = PlayniteApi.Database.Platforms.FirstOrDefault(platform => platform.SpecificationId == PC_WINDOWS);
            if (switchPlatform == null) switchPlatform = PlayniteApi.Database.Platforms.FirstOrDefault(platform => platform.SpecificationId == NINTENDO_SWITCH);

            logger.Trace("ProcessOrders: DB begin update");
            PlayniteApi.Database.BeginBufferUpdate();
            try
            {
                if (importProgress != null)
                {
                    importProgress.ProgressValue = 0f;
                    importProgress.Visible = true;
                }
                var currentOrderIndex = 0;
                foreach (var order in orders)
                {
                    var tpkdGroupEnumerable = order.tpkd_dict.all_tpks
                        .Where(t => t != null
                                    && Settings.keyTypeWhitelist.ContainsKey(t.key_type)
                                    && !string.IsNullOrWhiteSpace(t.gamekey))
                        .GroupBy(tpk => tpk.gamekey);
                    foreach (var tpkdGroup in tpkdGroupEnumerable)
                    {
                        var tpkdGroupEntries = tpkdGroup.AsEnumerable();
                        Tag humbleChoiceTag = null;
                        var groupEntries = tpkdGroupEntries.ToList();
                        if (Settings.ImportChoiceKeys && tagMethod != TagMethodology.None && groupEntries.Count() > 1)
                        {
                            var isHumbleMonthly = order.product.human_name.Contains("Humble Monthly");
                            if (tagMethod == TagMethodology.All || tagMethod == TagMethodology.Monthly && isHumbleMonthly)
                            {
                                humbleChoiceTag = PlayniteApi.Database.Tags.Add($"Bundle: {order.product.human_name}");
                            }
                        }

                        var bundleContainsUnredeemableKeys = false;
                        var sourceOrder = order;
                        if (sourceOrder != null && sourceOrder.product.category != "storefront" && sourceOrder.total_choices > 0 && sourceOrder.product.is_subs_v2_product)
                        {
                            bundleContainsUnredeemableKeys = sourceOrder.choices_remaining == 0;
                        }

                        // Monthly bundle has all choices made
                        if (bundleContainsUnredeemableKeys && humbleChoiceTag != null)
                        {
                            // search Playnite db for all games that are not included in groupEntries, these can be removed
                            var virtualOrders = order.tpkd_dict.all_tpks.Where(tpk => tpk.is_virtual).Select(GetGameId) ??
                                                new List<string>();
                            var gameKeys = virtualOrders.ToList();
                            // for this bundle, get all games from the database that are not in the keys collection for this order
                            var libraryKeysNotInOrder = PlayniteApi.Database.Games
                                .Where(game =>
                                    game.TagIds != null && game.TagIds.Contains(humbleChoiceTag.Id) && gameKeys.Contains(game.GameId))
                                .ToList();
                            foreach (var game in libraryKeysNotInOrder)
                            {
                                switch (unredeemableMethod)
                                {
                                    case UnredeemableMethodology.Tag:
                                    {
                                        game.TagIds.Remove(unredeemedTag.Id);
                                        if (game.TagIds.Contains(unredeemableTag.Id)) continue;

                                        game.TagIds.Add(unredeemableTag.Id);
                                        PlayniteApi.Notifications.Add(
                                            new NotificationMessage("HumbleKeysLibraryUpdate_" + game.Id,
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
                                    case UnredeemableMethodology.Delete:
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

                        foreach (var tpkd in groupEntries)
                        {
                            var gameId = GetGameId(tpkd);

                            var alreadyImported = PlayniteApi.Database.Games.FirstOrDefault(game => game.GameId == gameId && game.PluginId == Id);

                            if (alreadyImported == null)
                            {
                                if (!Settings.IgnoreRedeemedKeys || (Settings.IgnoreRedeemedKeys && !IsKeyPresent(tpkd)))
                                {
                                    importedGames.Add(ImportNewGame(tpkd, humbleChoiceTag));
                                }
                            }
                            else
                            {
                                if (!Settings.IgnoreRedeemedKeys || (Settings.IgnoreRedeemedKeys && !IsKeyPresent(tpkd)))
                                {
                                    var tagsUpdated = UpdateRedemptionStatus(alreadyImported, tpkd, humbleChoiceTag);
                                    var otherUpdated = UpdatePlatform(alreadyImported, tpkd);
                                    if (UpdateRedemptionStore(alreadyImported, tpkd)) otherUpdated = true;

                                    if (Settings.AddLinks)
                                    {
                                        if (alreadyImported.Links == null)
                                        {
                                            alreadyImported.Links = new ObservableCollection<Link>();
                                        }

                                        if (UpdateStoreLinks(alreadyImported.Links, tpkd, true)) otherUpdated = true;
                                    }

                                    if (!tagsUpdated && !otherUpdated)
                                    {
                                        logger.Trace($"ProcessOrders: No update needed for '{alreadyImported.Name}' with GameId = {alreadyImported.GameId}");
                                        continue;
                                    }

                                    if (alreadyImported.TagIds != null && alreadyImported.TagIds.Contains(unredeemableTag.Id))
                                    {
                                        switch (unredeemableMethod)
                                        {
                                            case UnredeemableMethodology.Tag:
                                            {
                                                PlayniteApi.Database.Games.Update(alreadyImported);
                                                PlayniteApi.Notifications.Add(
                                                    new NotificationMessage("HumbleKeysLibraryUpdate_" + alreadyImported.Id,
                                                        $"{alreadyImported.Name} is no longer redeemable", NotificationType.Info,
                                                        () =>
                                                        {
                                                            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
                                                                return;
                                                            PlayniteApi.MainView.SelectGame(alreadyImported.Id);
                                                        })
                                                );
                                                break;
                                            }
                                            case UnredeemableMethodology.Delete:
                                            {
                                                if (PlayniteApi.Database.Games.Remove(alreadyImported))
                                                {
                                                    removedGames.Add(alreadyImported);
                                                }

                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        PlayniteApi.Database.Games.Update(alreadyImported);
                                        logger.Trace($"ProcessOrders: Updated '{alreadyImported.Name}' with GameId = {alreadyImported.GameId}");
                                        if (tagsUpdated)
                                        {
                                            PlayniteApi.Notifications.Add(
                                                new NotificationMessage("HumbleKeysLibraryUpdate_" + alreadyImported.Id,
                                                    $"Tags Updated for {alreadyImported.Name}: " + GetOrderRedemptionTagState(tpkd), NotificationType.Info,
                                                    () =>
                                                    {
                                                        if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen) return;
                                                        PlayniteApi.MainView.SelectGame(alreadyImported.Id);
                                                    })
                                            );
                                        }
                                    }
                                }
                                else
                                {
                                    // Remove Existing Game?
                                    PlayniteApi.Database.Games.Remove(alreadyImported);
                                    logger.Trace(
                                        $"Removing game '{alreadyImported.Name}' with GameId = {alreadyImported.GameId} since Settings.IgnoreRedeemedKeys is: [{Settings.IgnoreRedeemedKeys}] and IsKeyPresent() is [{IsKeyPresent(tpkd)}]");
                                }
                            }
                        }
                    }

                    currentOrderIndex++;
                    if (importProgress == null) continue;
                    
                    importProgress.ProgressValue = ((float)currentOrderIndex / orderKeys.Count) * 100;
                }

                if (importProgress == null) return;
                
                importProgress.ProgressValue = 100f;
                importProgress.Visible = false;

            }
            finally
            {
                PlayniteApi.Database.EndBufferUpdate();
                logger.Trace("ProcessOrders: DB update complete");
            }
        }

        bool UpdateStoreLinks(ObservableCollection<Link> links, Order.TpkdDict.Tpk tpkd, bool useDispatcher)
        {
            var recordChanged = false;

            // add link to Humble purchase
            if (!string.IsNullOrWhiteSpace(tpkd?.gamekey))
            {
                var humbleLink = MakeLink(tpkd?.gamekey);

                if (!links.Contains(humbleLink))
                {
                    if (useDispatcher)
                    {
                        API.Instance.MainView.UIDispatcher.Invoke(delegate
                        {
                            links.Add(humbleLink);
                        });
                    }
                    else
                    {
                        links.Add(humbleLink);
                    }

                    recordChanged = true;
                }
            }

            if (tpkd.key_type != "steam") return recordChanged;

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
                if (useDispatcher)
                {
                    API.Instance.MainView.UIDispatcher.Invoke(delegate
                    {
                        links.Add(steamGameLink);
                    });
                }
                else
                {
                    links.Add(steamGameLink);
                }

                return true;
            }

            if (!string.IsNullOrEmpty(tpkd.steam_app_id) && existingSteamLink.Url == steamGameLink.Url) return recordChanged;

            // steam link url doesn't match expected value
            if (existingSteamLink.Url != steamGameLink.Url)
            {
                existingSteamLink.Url = steamGameLink.Url;
            }
            else
            {
                return recordChanged;
            }

            return true;
        }

        Game ImportNewGame(Order.TpkdDict.Tpk tpkd, Tag groupTag = null)
        {
            var gameInfo = new GameMetadata()
            {
                Name = tpkd.human_name,
                GameId = GetGameId(tpkd),
            };

            if (Settings.RedemptionStore != (int)RedemptionStoreType.Source)
            {
                gameInfo.Source = new MetadataNameProperty(HUMBLE_KEYS_SRC_NAME);
            }

            if (Settings.AddKeyStatus)
            {
                // add tag reflecting redemption status
                gameInfo.Tags = new HashSet<MetadataProperty> { new MetadataNameProperty(GetOrderRedemptionTagState(tpkd)) };
            }

            if (Settings.AddLinks)
            {
                var links = new ObservableCollection<Link>();
                if (UpdateStoreLinks(links, tpkd, false))
                {
                    gameInfo.Links = new List<Link>();
                    gameInfo.Links.AddRange(links.ToList());
                }
            }

            // no need to call BeginBufferUpdate() here because the only place this method is called already did that
            var game = PlayniteApi.Database.ImportGame(gameInfo, this);
            var gameChanged = false;

            if (groupTag != null)
            {
                EnsureTagList(game);
                game.TagIds.Add(groupTag.Id);
                gameChanged = true;
            }

            if (UpdatePlatform(game, tpkd)) gameChanged = true;
            if (UpdateRedemptionStore(game, tpkd)) gameChanged = true;

            if (gameChanged)
            {
                PlayniteApi.Database.Games.Update(game);
            }

            logger.Trace($"ImportNewGame: Added '{game.Name}' with GameId = {game.GameId}");
            return game;
        }

        // If a game is expired, add tag 'Key: Unredeemable'
        // If a game had been redeemed since last added to Playnite, remove the tag 'Key: Unredeemed' and add the tag 'Key: Redeemed'
        // returns whether tags were updated or not
        bool UpdateRedemptionStatus(Game existingGame, Order.TpkdDict.Tpk tpkd, Tag groupTag = null)
        {
            var recordChanged = false;
            if (existingGame == null) { return false; }
            if (!Settings.keyTypeWhitelist.ContainsKey(tpkd.key_type)) { return false; }

            if (groupTag != null)
            {
                if (existingGame.Tags == null || existingGame.Tags.All(tag => tag.Id != groupTag.Id))
                {
                    EnsureTagList(existingGame);
                    existingGame.TagIds.Add(groupTag.Id);
                    recordChanged = true;
                }
            }

            if (!Settings.AddKeyStatus) return recordChanged;

            // process tags on existingGame only if there was a change in tag status
            var existingRedemptionTagIds = existingGame.Tags?.Where(t => PAST_TAGS.Contains(t.Name)).ToList().Select(tag => tag.Id)??Enumerable.Empty<Guid>();
            
            // This creates a new Tag in the Tag Database if it doesn't already exist for 'Tag: Redeemed'
            var tagIds = existingRedemptionTagIds.ToList();
            // no need to call BeginBufferUpdate() here because the only place this method is called already did that
            var currentTagState = PlayniteApi.Database.Tags.Add(GetOrderRedemptionTagState(tpkd));

            // existingGame already tagged with correct tag state
            if (tagIds.Contains(currentTagState.Id)) return recordChanged;

            if (existingGame.TagIds == null)
            {
                existingGame.TagIds = new List<Guid>();
            }
            else
            {
                // remove all tags related to key state
                existingGame.TagIds.RemoveAll(tagId => tagIds.Contains(tagId));
            }

            existingGame.TagIds.Add(currentTagState.Id);

            return true;
        }

        // Add Platform if needed
        // returns whether it was updated or not 
        bool UpdatePlatform(Game game, Order.TpkdDict.Tpk tpkd)
        {
            var recordChanged = false;

            if (tpkd.key_type == "nintendo_direct")
            {
                // Add "Nintendo Switch" for all Nintendo keys
                if (Settings.AddPlatformNintendo)
                {
                    if (game.Platforms?.FirstOrDefault(platform => platform.SpecificationId == NINTENDO_SWITCH) == null)
                    {
                        EnsurePlatformList(game);
                        game.PlatformIds.Add(switchPlatform.Id);
                        recordChanged = true;
                    }
                }
            }
            else
            {
                // Add default "PC (Windows)" for all other keys
                if (Settings.AddPlatformWindows)
                {
                    if (game.Platforms?.FirstOrDefault(platform => platform.SpecificationId == PC_WINDOWS) == null)
                    {
                        EnsurePlatformList(game);
                        game.PlatformIds.Add(winPlatform.Id);
                        recordChanged = true;
                    }
                }
            }

            return recordChanged;
        }

        // Add Redemption Store if needed
        // returns whether it was updated or not
        bool UpdateRedemptionStore(Game game, Order.TpkdDict.Tpk tpkd)
        {
            if (Settings.RedemptionStore == (int)RedemptionStoreType.None) return false;
            var recordChanged = false;
            var newSource = GetKeyInfo(tpkd.key_type, Settings.RedemptionStore == (int)RedemptionStoreType.Source);
            string newName = HUMBLE_KEYS_PLATFORM_NAME + newSource.Name;

            switch (Settings.RedemptionStore)
            {
                case (int)RedemptionStoreType.Source:
                    if (game.SourceId != newSource.SourceId)
                    {
                        game.SourceId = newSource.SourceId;
                        recordChanged = true;
                    }

                    break;
                case (int)RedemptionStoreType.Tag:
                    var newTag = PlayniteApi.Database.Tags.FirstOrDefault(tag => tag.Name == newName) ?? PlayniteApi.Database.Tags.Add(newName);
                    EnsureTagList(game);

                    if (!game.TagIds.Contains(newTag.Id))
                    {
                        game.TagIds.Add(newTag.Id);
                        recordChanged = true;
                    }

                    break;
                case (int)RedemptionStoreType.Category:
                    var newCat = PlayniteApi.Database.Categories.FirstOrDefault(category => category.Name == newName) ?? PlayniteApi.Database.Categories.Add(newName);
                    EnsureCategoryList(game);

                    if (!game.CategoryIds.Contains(newCat.Id))
                    {
                        game.CategoryIds.Add(newCat.Id);
                        recordChanged = true;
                    }

                    break;
                case (int)RedemptionStoreType.Platform:
                    var newPlat = PlayniteApi.Database.Platforms.FirstOrDefault(platform => platform.Name == newName) ?? PlayniteApi.Database.Platforms.Add(newName);

                    EnsurePlatformList(game);
                    if (!game.PlatformIds.Contains(newPlat.Id))
                    {
                        game.PlatformIds.Add(newPlat.Id);
                        recordChanged = true;
                    }

                    break;
            }

            return recordChanged;
        }

        KeyInfo GetKeyInfo(string key_type, bool needSourceId)
        {
            if (Settings.keyTypeWhitelist.TryGetValue(key_type, out KeyInfo keyInfo))
            {
                if (needSourceId && keyInfo.SourceId == Guid.Empty)
                {
                    var source = PlayniteApi.Database.Sources.FirstOrDefault(src => src.Name == keyInfo.SourceName) ?? PlayniteApi.Database.Sources.Add(new MetadataNameProperty(keyInfo.SourceName));
                    keyInfo.SourceId = source.Id;
                }

                return keyInfo;
            }
            else
            {
                if (needSourceId && humbleKeysSource.SourceId == Guid.Empty)
                {
                    var source = PlayniteApi.Database.Sources.FirstOrDefault(src => src.Name == HUMBLE_KEYS_SRC_NAME) ?? PlayniteApi.Database.Sources.Add(new MetadataNameProperty(HUMBLE_KEYS_SRC_NAME));
                    humbleKeysSource.SourceId = source.Id;
                }

                return humbleKeysSource;
            }
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
            return IsKeyPresent(t) ? REDEEMED_STR : UNREDEEMED_STR;
        }
        private static void EnsureTagList(Game game)
        {
            if (game.TagIds == null) game.TagIds = new List<Guid>();
        }
        private static void EnsurePlatformList(Game game)
        {
            if (game.PlatformIds == null) game.PlatformIds = new List<Guid>();
        }
        private static void EnsureCategoryList(Game game)
        {
            if (game.CategoryIds == null) game.CategoryIds = new List<Guid>();
        }
        #endregion
    }
}