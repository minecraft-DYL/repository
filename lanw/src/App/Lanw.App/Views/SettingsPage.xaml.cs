using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Lanw.App.Views
{
    /// <summary>
    /// 设置页：本地配置项（对应原 Vue Settings.vue），绑定 LanwConfig 持久化设置。
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; } = new();

        public SettingsPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
        }
    }
}
