using System.Windows.Controls;

namespace LoneEftDmaRadar.UI.Hotkeys
{
    /// <summary>
    /// Hotkey Manager embedded tab (reuses the same view model as the popup).
    /// </summary>
    public partial class HotkeyManagerTab : UserControl
    {
        public HotkeyManagerTab()
        {
            InitializeComponent();
            DataContext = new HotkeyManagerViewModel();
        }
    }
}
