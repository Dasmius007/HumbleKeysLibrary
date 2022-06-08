using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using HumbleKeys.Models;

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

        //public override LibraryPluginCapabilities Capabilities { get; } = new LibraryPluginCapabilities
        //{
        //    CanShutdownClient = false,
        //    HasCustomizedGameImport = true
        //};

        public override Guid Id { get; } = Guid.Parse("62ac4052-e08a-4a1a-b70a-c2c0c3673bb9");
        public override string Name => "Humble Keys Library";

        // Implementing Client adds ability to open it via special menu in playnite.
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


        //public override IEnumerable<Game> ImportGames()
        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)

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
                logger.Error($"HKL: error {e}");
            }

            if (importError != null)
            {
                logger.Error($"HKL: importError {dbImportMessageId}");
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
            //foreach(var o in orders)
            //{
            //    if (o == null) { continue; }

            //    foreach(var t in o.tpkd_dict.all_tpks)
            //    {
            //        if (t == null) { continue; }

            //        logger.Debug($"GAMES; key = {o.gamekey}; product = {o.product.human_name}; tpk = {t.human_name}; key type = {t.key_type}; redeemed = {GetRedeemedTag(t)}");
            //    }
            //}

            return orders
                .SelectMany(a => a.tpkd_dict?.all_tpks)
                .Where(t => t != null
                    && Settings.keyTypeWhitelist.Contains(t.key_type)
                    && !string.IsNullOrWhiteSpace(t.gamekey)
                    && !(Settings.IgnoreRedeemedKeys && IsKeyPresent(t) )
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
        }

        private Game ImportNewGame(Models.Order.TpkdDict.Tpk tpkd)
        {
            //GameInfo gameInfo = new GameInfo()
            //{
            //    Name = tpkd.human_name,
            //    GameId = GetGameId(tpkd),
            //    Platform = HUMBLE_KEYS_PLATFORM_NAME + tpkd.key_type,
            //    Source = HUMBLE_KEYS_SRC_NAME,
            //    Tags = new List<string>(),
            //    Links = new List<Link>(),
            //};

            

            //var tempName = tpkd.human_name;
            //var tempGameId = GetGameId(tpkd);
            //var tempKeyType = tpkd?.key_type;
            //var tempPlatforms = new HashSet<MetadataProperty> { new MetadataNameProperty(
            //            HUMBLE_KEYS_PLATFORM_NAME + tempKeyType) };
            //var tempSource = new MetadataNameProperty(HUMBLE_KEYS_SRC_NAME);

            //GameMetadata gameInfo;

            //try
            //{
            GameMetadata gameInfo = new GameMetadata()
            {
                Name = tpkd.human_name,
                GameId = GetGameId(tpkd),
                Source = new MetadataNameProperty(HUMBLE_KEYS_SRC_NAME),
                Platforms = new HashSet<MetadataProperty> { new MetadataNameProperty(
                        HUMBLE_KEYS_PLATFORM_NAME + tpkd.key_type) },
                Tags = new HashSet<MetadataProperty>(),
                Links = new List<Link>(),
            };
            //}
            //catch (Exception e)
            //{
            //    logger.Debug($"DEBUG === {tpkd.human_name} GameMetadata CATCH === " + e.ToString());
            //    gameInfo = null;
            //}

            //gameInfo = new GameMetadata()
            //{
            //    Name = tempName,
            //    GameId = tempGameId,
            //    Icon = null,
            //    Source = tempSource,
            //};

            //{
            //    Name = tpkd?.human_name ?? "Null tpkd, no name imported",
            //    GameId = GetGameId(tpkd),
            //    Platforms = new HashSet<MetadataProperty> { new MetadataNameProperty(
            //            HUMBLE_KEYS_PLATFORM_NAME + (string)tpkd?.key_type ) },
            //    Source = new MetadataNameProperty(HUMBLE_KEYS_SRC_NAME),
            //    Tags = new HashSet<MetadataProperty>(),
            //    Links = new List<Link>(),
            //};

            //if (tpkd.redeemed_key_val.Type == Newtonsoft.Json.Linq.JTokenType.String)
            //{
            //    gameInfo.Tags.Add(new MetadataNameProperty(REDEEMED_STR));
            //}
            //else if (tpkd.redeemed_key_val.Type == Newtonsoft.Json.Linq.JTokenType.Object &&
            //    tpkd.redeemed_key_val["key"].Type == Newtonsoft.Json.Linq.JTokenType.String)
            //{

            //}

            //gameInfo.Tags.Add(string.IsNullOrEmpty(tpkd.redeemed_key_val) ? UNREDEEMED_STR : REDEEMED_STR);


            //string redemptionTag = string.IsNullOrEmpty(tpkd?.redeemed_key_val?.ToString()) ? UNREDEEMED_STR : REDEEMED_STR;

            gameInfo.Tags.Add(new MetadataNameProperty(GetRedeemedTag(tpkd)));
            

            //gameInfo.Tags.Add(new MetadataNameProperty(
            //    string.IsNullOrEmpty(tpkd.redeemed_key_val.ToString()) ? UNREDEEMED_STR : REDEEMED_STR
            //));

            if (!string.IsNullOrWhiteSpace(tpkd?.gamekey))
            {
                gameInfo.Links.Add(MakeLink(tpkd?.gamekey));
            }

            return PlayniteApi.Database.ImportGame(gameInfo, this);
        }


        private void UpdateExistingGame(Game existingGame, Order.TpkdDict.Tpk tpkd)
        {
            if (existingGame == null) { return; }
            if (!Settings.keyTypeWhitelist.Contains(tpkd.key_type)) { return; }

            existingGame.Tags.RemoveAll(t => PAST_TAGS.Contains(t.Name));

            existingGame.Tags.Add(new Tag( IsKeyPresent(tpkd) ? REDEEMED_STR : UNREDEEMED_STR ));

            PlayniteApi.Database.Games.Update(existingGame);
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