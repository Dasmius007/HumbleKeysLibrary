using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using HumbleKeys.Models;

namespace HumbleKeys
{
    public class HumbleKeysLibrary : LibraryPlugin
    {
        #region === Constants ================
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string dbImportMessageId = "humblekeyslibImportError";
        private const string humblePurchaseUrlMask = @"https://www.humblebundle.com/downloads?key={0}";
        private const string REDEEMED_STR = "Redeemed";
        private const string UNREDEEMED_STR = "Unredeemed";
        private const string HUMBLE_KEYS_SRC_NAME = "Humble Keys";
        private const string HUMBLE_KEYS_PLATFORM_NAME = "Humble Key: ";
        #endregion

        #region === Accessors ================
        private HumbleKeysLibrarySettings Settings { get; set; }

        public override LibraryPluginCapabilities Capabilities { get; } = new LibraryPluginCapabilities
        {
            CanShutdownClient = false,
            HasCustomizedGameImport = true
        };

        public override Guid Id { get; } = Guid.Parse("62ac4052-e08a-4a1a-b70a-c2c0c3673bb9");
        public override string Name => "Humble Keys Library";

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client { get; } = new HumbleKeysLibraryClient();
        #endregion


        public HumbleKeysLibrary(IPlayniteAPI api) : base(api)
        {
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
        

        public override IEnumerable<Game> ImportGames()
        {
            var importedGames = new List<Game>();
            Exception importError = null;
            if (!Settings.ConnectAccount) { return importedGames; }

            try
            {
                var orders = ScrapeOrders();
                var selectedTpkds = SelectTpkds(orders);
                TpkdsIntake(selectedTpkds, ref importedGames);
            }
            catch (Exception e)
            {
                importError = e;
            }

            if (importError != null)
            {
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


        public List<Order> ScrapeOrders()
        {
            var orders = new List<Order>();
            using (var view = PlayniteApi.WebViews.CreateOffscreenView(
                    new WebViewSettings { JavaScriptEnabled = false }))
            {
                var api = new Services.HumbleKeysAccountClient(view);
                var keys = api.GetLibraryKeys();
                orders = api.GetOrders(keys);
            }

            return orders;
        }


        public List<Order.TpkdDict.Tpk> SelectTpkds(List<Order> orders)
        {
            return orders
                .SelectMany(a => a.tpkd_dict?.all_tpks)
                .Where(t => t != null
                    && Settings.keyTypeWhitelist.Contains(t.key_type)
                    && !string.IsNullOrWhiteSpace(t.gamekey)
                )
                .ToList();
        }


        protected void TpkdsIntake(List<Order.TpkdDict.Tpk> tpkds, ref List<Game> importedGames)
        {
            foreach (var tpkd in tpkds)
            {
                string gameId = GetGameId(tpkd);

                Game alreadyImported = 
                    PlayniteApi.Database.Games.FirstOrDefault(
                        g => g.GameId == GetGameId(tpkd) && g.PluginId == Id);

                if (alreadyImported == null)
                {
                    importedGames.Add(ImportNewGame(tpkd));
                }
                else
                {
                    UpdateExistingGame(alreadyImported, tpkd);
                }
            }

            logger.Info($"Imported {importedGames.Count} Humble TPKDs.");
        }

        private Game ImportNewGame(Models.Order.TpkdDict.Tpk tpkd)
        {
            GameInfo gameInfo = new GameInfo()
            {
                Name = tpkd.human_name,
                GameId = GetGameId(tpkd),
                Platform = HUMBLE_KEYS_PLATFORM_NAME + tpkd.key_type,
                Source = HUMBLE_KEYS_SRC_NAME,
                Tags = new List<string>(),
                Links = new List<Link>(),
            };

            gameInfo.Tags.Add(string.IsNullOrEmpty(tpkd.redeemed_key_val) ? UNREDEEMED_STR : REDEEMED_STR);

            if (!string.IsNullOrWhiteSpace(tpkd.gamekey))
            {
                gameInfo.Links.Add(MakeLink(tpkd.gamekey));
            }

            return PlayniteApi.Database.ImportGame(gameInfo, this);
        }


        private void UpdateExistingGame(Game existingGame, Order.TpkdDict.Tpk tpkd)
        {
            if (existingGame == null) { return; }
            if (!Settings.keyTypeWhitelist.Contains(tpkd.key_type)) { return; }
            
            // if tpkd is redeemed but game isn't, add redeemed, remove unredeemed
            if (!string.IsNullOrWhiteSpace(tpkd.redeemed_key_val)
                && !existingGame.Tags.Any(t => t.Name == REDEEMED_STR))
            {
                existingGame.Tags.RemoveAll(t => t.Name == UNREDEEMED_STR);
                existingGame.Tags.Add(new Tag(REDEEMED_STR));
            }

            // if tpkd is unredeemed but game isn't, add unredeemed, remove redeemed
            // this should be impossible
            if (string.IsNullOrWhiteSpace(tpkd.redeemed_key_val)
                && !existingGame.Tags.Any(t => t.Name == UNREDEEMED_STR))
            {
                existingGame.Tags.RemoveAll(t => t.Name == REDEEMED_STR);
                existingGame.Tags.Add(new Tag(UNREDEEMED_STR));
            }

            PlayniteApi.Database.Games.Update(existingGame);
        }


        #region === Helper Methods ============
        private static string GetGameId(Order.TpkdDict.Tpk tpk) => $"{tpk.machine_name}_{tpk.gamekey}";
        private static Link MakeLink(string gamekey) => new Link("Humble Purchase URL", string.Format(humblePurchaseUrlMask, gamekey) );
        #endregion
    }
}