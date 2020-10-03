using Newtonsoft.Json;
using Playnite.SDK;
using HumbleKeys.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using HumbleKeys.Services;


namespace HumbleKeys
{
    public class HumbleKeysLibrarySettings : ObservableObject, ISettings
    {
        private readonly HumbleKeysLibrary plugin;
        private static ILogger logger = LogManager.GetLogger();
        private HumbleKeysLibrarySettings editingClone;

        public bool ConnectAccount { get; set; } = false;

        public List<string> keyTypeWhitelist = new List<string>() {
            "gog",
            "nintendo_direct",
            "origin",
            "origin_keyless",
            "steam",
        };

        [JsonIgnore]
        public bool IsUserLoggedIn
        {
            get
            {
                using (var view = plugin.PlayniteApi.WebViews.CreateOffscreenView(
                    new WebViewSettings
                    {
                        JavaScriptEnabled = false
                    }))
                {
                    var api = new HumbleKeysAccountClient(view);
                    return api.GetIsUserLoggedIn();
                }
            }
        }

        [JsonIgnore]
        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public HumbleKeysLibrarySettings()
        {
        }

        public HumbleKeysLibrarySettings(HumbleKeysLibrary plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<HumbleKeysLibrarySettings>();

            if (savedSettings != null)
            {
                LoadValues(savedSettings);
            }
        }

        public void BeginEdit()
        {
            editingClone = this.GetClone();
        }

        public void CancelEdit()
        {
            LoadValues(editingClone);
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(this);
        }

        private void LoadValues(HumbleKeysLibrarySettings source)
        {
            source.CopyProperties(this, false, null, true);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        private void Login()
        {
            try
            {
                using (var view = plugin.PlayniteApi.WebViews.CreateView(490, 670))
                {
                    var api = new HumbleKeysAccountClient(view);
                    api.Login();
                }

                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                logger.Error(e, "Failed to authenticate user.");
            }
        }

    }
}