using System.Windows;
using System.Windows.Controls;

namespace HumbleKeys
{
    public partial class HumbleKeysLibrarySettingsView : UserControl
    {
        public HumbleKeysLibrarySettingsView()
        {
            InitializeComponent();
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