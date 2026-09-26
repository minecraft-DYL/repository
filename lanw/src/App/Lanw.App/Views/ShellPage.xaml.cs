using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Lanw.App.Views
{
    /// <summary>
    /// 应用主壳：NavigationView 导航框架。
    /// 缺省用户的主入口为账号管理页（AccountsPage）——登录已并入该页的「添加账号」按钮，
    /// 原独立的「登录」页（LoginPage）已按用户要求删除，导航框架驱动各页切换。
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        public ShellPage()
        {
            this.InitializeComponent();
            navView.ItemInvoked += NavView_ItemInvoked;
        }

        private void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 启动缺省主入口：contentFrame 初始为空时自动导航到账号管理页并选中对应菜单项，无需用户点击。
            // 注意：程序化设置 SelectedItem 不会触发 ItemInvoked，因此必须显式导航 contentFrame。
            if (contentFrame.Content is not null)
            {
                return; // 已有内容（如 Loaded 再次触发），保持现状。
            }

            navView.SelectedItem = accountsItem;
            NavigateToPage(typeof(AccountsPage));
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            object? tag = null;
            bool isSettings = args.IsSettingsInvoked;

            if (args.InvokedItemContainer is NavigationViewItem item)
            {
                tag = item.Tag;
            }

            var targetPage = ResolveTarget(tag as string, isSettings);
            if (targetPage is null)
            {
                return;
            }

            NavigateToPage(targetPage);
        }

        private void NavigateToPage(Type targetPage)
        {
            if (contentFrame.Content?.GetType() == targetPage)
            {
                return; // 已在目标页
            }

            contentFrame.Navigate(targetPage, null, new SuppressNavigationTransitionInfo());
        }

        private static Type? ResolveTarget(string? tag, bool isSettings)
        {
            if (isSettings)
            {
                return typeof(SettingsPage);
            }

            return tag switch
            {
                "home" => typeof(HomePage),
                "accounts" => typeof(AccountsPage),
                "launch" => typeof(GameLaunchManagerPage),
                "servers" => typeof(ServersPage),
                "skins" => typeof(SkinsPage),
                "rental" => typeof(GameRentalPage),
                "proxy" => typeof(ProxyManagerPage),
                "plugins" => typeof(PluginsPage),
                "pluginstore" => typeof(PluginStorePage),
                "chat" => typeof(ChatPage),
                "logs" => typeof(LogsPage),
                "version" => typeof(VersionPage),
                "userhome" => typeof(UserHomePage),
                "settings" => typeof(SettingsPage),
                _ => null,
            };
        }
    }
}