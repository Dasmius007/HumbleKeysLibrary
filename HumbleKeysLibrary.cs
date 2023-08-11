using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
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
        private const string REDEEMED_STR = "Key: Redeemed";
        private const string UNREDEEMED_STR = "Key: Unredeemed";
        private static readonly string[] PAST_TAGS = { REDEEMED_STR, UNREDEEMED_STR, "Redeemed", "Unredeemed", };
        private const string HUMBLE_KEYS_SRC_NAME = "Humble Keys";
        private const string HUMBLE_KEYS_PLATFORM_NAME = "Humble Key: ";
        #endregion

        #region === Accessors ================
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
            Exception importError = null;

            if (!Settings.ConnectAccount) { return importedGames; }

            try
            {
                var orders = ScrapeOrders();
                var selectedTpkds = SelectTpkds(orders);
                TpkdsIntake(orders, selectedTpkds, ref importedGames);
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
                            && !(Settings.IgnoreRedeemedKeys && IsKeyPresent(t))
                ).GroupBy(tpk => tpk.gamekey);
        }


        protected void TpkdsIntake(Dictionary<string,Order> orders, IEnumerable<IGrouping<string, Order.TpkdDict.Tpk>> tpkds, ref List<Game> importedGames)
        {
            foreach (var tpkdGroup in tpkds)
            {
                var tpkdGroupEntries = tpkdGroup.AsEnumerable();
                Tag groupTag = null;
                var groupEntries = tpkdGroupEntries.ToList();
                if (Settings.ImportChoiceKeys && Settings.CurrentTagMethodology != "none" && groupEntries.Count() > 1)
                {
                    var isHumbleMonthly = orders[tpkdGroup.Key].product.human_name.Contains("Humble Monthly");
                    if (Settings.CurrentTagMethodology == "all" || Settings.CurrentTagMethodology == "monthly" && isHumbleMonthly)
                    {
                        groupTag = PlayniteApi.Database.Tags.Add($"Bundle: {orders[tpkdGroup.Key].product.human_name}");
                    }
                }
                
                foreach (var tpkd in groupEntries)
                {
                    var gameId = GetGameId(tpkd);

                    var alreadyImported = 
                        PlayniteApi.Database.Games.FirstOrDefault(
                            g => g.GameId == GetGameId(tpkd) && g.PluginId == Id);

                    if (alreadyImported == null)
                    {
                        importedGames.Add(ImportNewGame(tpkd, groupTag));
                    }
                    else
                    {
                        UpdateExistingGame(alreadyImported, tpkd, groupTag);
                    }
                }
            }
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
            
            gameInfo.Tags.Add(new MetadataNameProperty(GetRedeemedTag(tpkd)));
            if (!string.IsNullOrWhiteSpace(tpkd?.gamekey))
            {
                gameInfo.Links.Add(MakeLink(tpkd?.gamekey));
            }
            PlayniteApi.Database.BeginBufferUpdate();
            var game = PlayniteApi.Database.ImportGame(gameInfo, this);
            if (groupTag != null)
            {
                game.Tags.Add(groupTag);
                PlayniteApi.Database.Games.Update(game);
            }
            PlayniteApi.Database.EndBufferUpdate();
            return game;
        }

        void UpdateExistingGame(Game existingGame, Order.TpkdDict.Tpk tpkd, Tag groupTag = null)
        {
            if (existingGame == null) { return; }
            if (!Settings.keyTypeWhitelist.Contains(tpkd.key_type)) { return; }

            var existingTagIds = existingGame.Tags.Where(t => PAST_TAGS.Contains(t.Name)).Select(tag => tag.Id);
            existingGame.TagIds.RemoveAll(tagId => existingTagIds.Contains(tagId));

            PlayniteApi.Database.BeginBufferUpdate();
            existingGame.TagIds.Add(PlayniteApi.Database.Tags.Add(IsKeyPresent(tpkd) ? REDEEMED_STR : UNREDEEMED_STR ).Id);
            if (groupTag != null)
            {
                if (existingGame.Tags.All(tag => tag.Id != groupTag.Id))
                {
                    existingGame.TagIds.Add(groupTag.Id);
                }
            }
            PlayniteApi.Database.Games.Update(existingGame);
            PlayniteApi.Database.EndBufferUpdate();
        }

        #region === Helper Methods ============
        private static string GetGameId(Order.TpkdDict.Tpk tpk) => $"{tpk.machine_name}_{tpk.gamekey}";
        private static Link MakeLink(string gamekey) => new Link("Humble Purchase URL", string.Format(humblePurchaseUrlMask, gamekey) );
        private static bool IsKeyNull(Order.TpkdDict.Tpk t) => t?.redeemed_key_val == null;
        private static bool IsKeyPresent(Order.TpkdDict.Tpk t) => !IsKeyNull(t);
        private static string GetRedeemedTag(Order.TpkdDict.Tpk t) => IsKeyPresent(t) ? REDEEMED_STR : UNREDEEMED_STR;
        #endregion
    }
}