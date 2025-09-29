using Playnite.SDK;
using HumbleKeys.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using HumbleKeys.Services;
using Playnite.SDK.Data;

namespace HumbleKeys
{
    public enum RedemptionStoreType
    {
        None,       // 0
        Source,     // 1
        Tag,        // 2
        Category,   // 3
        Platform,   // 4
    }

    public enum TagMethodology
    {
        None,       // 0
        Monthly,    // 1
        All,        // 2
    }

    public enum UnredeemableMethodology
    {
        Tag,        // 0
        Delete,     // 1
    }

    public class KeyInfo
    {
        public string Name { get; set; }        // Description used anywhere else needed, other than for Playnite's "Source" field
        public string SourceName { get; set; }  // Exact match for Playnite's "Source" field, only used when Redemption Store is saved to Source
        public Guid SourceId { get; set; }      // "Source" field GUID found in Playnite
    }

    public class HumbleKeysLibrarySettings : ObservableObject, ISettings
    {
        private readonly HumbleKeysLibrary plugin;
        private static readonly ILogger logger = LogManager.GetLogger();
        private HumbleKeysLibrarySettings editingClone;

        public bool ConnectAccount { get; set; } = false;
        public bool IgnoreRedeemedKeys { get; set; } = false;
        public bool AddKeyStatus { get; set; } = true;
        public int RedemptionStore { get; set; } = (int)RedemptionStoreType.Source;
        public bool AddLinks { get; set; } = true;
        public bool ImportChoiceKeys { get; set; } = false;
        public int TagWithBundleName { get; set; } = (int)TagMethodology.None;
        public int UnredeemableKeyHandling { get; set; } = (int)UnredeemableMethodology.Tag;
        public bool CacheEnabled { get; set; } = false;

        [Obsolete("Deprecated, scheduled for deletion: Use TagWithBundleName instead")]
        public string CurrentTagMethodology { get; set; }
        [Obsolete("Deprecated, scheduled for deletion: Use UnredeemableKeyHandling instead")]
        public string CurrentUnredeemableMethodology { get; set; }
        public bool AddPlatformNintendo { get; set; } = true;
        public bool AddPlatformWindows { get; set; } = true;

        [DontSerialize]
        public Dictionary<string, KeyInfo> keyTypeWhitelist = new Dictionary<string, KeyInfo>
        {
            //["epic"] = new KeyInfo { Name = "Epic", SourceName = "Epic" },                  // This key type is valid so do we want to add it? I only have game dev asset keys at Epic myself right now but I assume this could be real games too
            //["epic_keyless"] = new KeyInfo { Name = "Epic keyless", SourceName = "Epic" },  // Is this even a valid key type? I just guessed it might be because Humble mentions it has keyless Epic keys
            ["gog"] = new KeyInfo { Name = "GOG", SourceName = "GOG" },
            ["nintendo_direct"] = new KeyInfo { Name = "Nintendo", SourceName = "Nintendo" },
            ["origin"] = new KeyInfo { Name = "EA", SourceName = "EA app" },
            ["origin_keyless"] = new KeyInfo { Name = "EA keyless", SourceName = "EA app" },
            ["steam"] = new KeyInfo { Name = "Steam", SourceName = "Steam" },
        };

        [DontSerialize]
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

        [DontSerialize]
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
                // Migrate old setting strings to enum ints; This code section is scheduled for deletion in the future
                if (savedSettings.CurrentTagMethodology != null || savedSettings.CurrentUnredeemableMethodology != null)
                {
                    switch (savedSettings.CurrentTagMethodology)
                    {
                        //case "none":  // None is default, so already correct
                        case "monthly":
                            savedSettings.TagWithBundleName = (int)TagMethodology.Monthly;
                            break;
                        case "all":
                            savedSettings.TagWithBundleName = (int)TagMethodology.All;
                            break;
                    }

                    // Tag is default, so no need to fix
                    if (savedSettings.CurrentUnredeemableMethodology == "delete")
                    {
                        savedSettings.UnredeemableKeyHandling = (int)UnredeemableMethodology.Delete;
                    }

                    // Clear deprecated values so migration only happens once
                    savedSettings.CurrentTagMethodology = null;
                    savedSettings.CurrentUnredeemableMethodology = null;

                    // Save, otherwise values won't stick
                    plugin.SavePluginSettings(savedSettings);
                }
                // End settings migration section


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