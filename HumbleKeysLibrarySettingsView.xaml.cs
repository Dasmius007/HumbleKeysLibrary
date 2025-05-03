using Playnite.SDK;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HumbleKeys
{
    public partial class HumbleKeysLibrarySettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public HumbleKeysLibrarySettingsView()
        {
            InitializeComponent();
            LoadLocalizedResources();
        }

        private void LoadLocalizedResources()
        {
            // Get the current culture's ISO language code (e.g., "en", "fr", "es")
            //string cultureCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // Get the current culture's country and language code (e.g., "en-US", "fr-FR", "es-ES")
            string cultureName = CultureInfo.CurrentUICulture.Name;

            if (cultureName == "en-US")
            {
                // No need to load resources for US English because it is already loaded via XAML (so it works in the designer as well)
                return;
            }

            // Construct the resource dictionary path based on the culture
            string resourcePath = $"pack://application:,,,/HumbleKeysLibrary;component/Localization/{cultureName}.xaml";

            try
            {
                // Load the resource dictionary
                var resourceDictionary = new ResourceDictionary
                {
                    Source = new Uri(resourcePath, UriKind.Absolute)
                };

                // Merge the resource dictionary into the UserControl's resources
                this.Resources.MergedDictionaries.Clear();
                this.Resources.MergedDictionaries.Add(resourceDictionary);
            }
            catch (Exception ex)
            {
                // No need to load fallback language because it is already loaded via XAML

                /*
                // Fallback to default resources (US English) if the specific culture file is not found
                string fallbackPath = "pack://application:,,,/HumbleKeysLibrary;component/Localization/en-US.xaml";
                var fallbackDictionary = new ResourceDictionary
                {
                    Source = new Uri(fallbackPath, UriKind.Absolute)
                };

                this.Resources.MergedDictionaries.Clear();
                this.Resources.MergedDictionaries.Add(fallbackDictionary);
                */

                // Log the error
                logger.Info($"Failed to load resources for culture '{cultureName}': {ex.Message}");
            }
        }

        void ImportChoiceKeys_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is HumbleKeysLibrarySettings model)) return;
            if (model.CurrentTagMethodology != "monthly") return;
            model.CurrentTagMethodology = "none";
            if (!(FindName("TagMethodology") is ListBox listBox)) return;
            foreach (ListBoxItem listBoxItem in listBox.Items)
            {
                listBoxItem.IsSelected = (string)listBoxItem.Tag == model.CurrentTagMethodology;
            }
        }
    }
}