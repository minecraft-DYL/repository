using Lanw.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lanw.App.Views
{
    /// <summary>
    /// 代理页：代理配置（拦截器参数 / 脱盒拦截开关）+ 运行状态（当前运行的代理列表）+ 拦截器启停。
    /// 数据来自进程内 Lanw.Development（t20 拦截器）与 Lanw.Public.Entities.NEL.RunningProxy（t14），无 HTTP。
    /// 主题跟随系统（根 Frame ElementTheme.Default）。
    /// </summary>
    public sealed partial class ProxyManagerPage : Page
    {
        public ProxyManagerViewModel ViewModel { get; } = new();

        public ProxyManagerPage()
        {
            this.InitializeComponent();
            Loaded += ProxyManagerPage_Loaded;
            Unloaded += ProxyManagerPage_Unloaded;
        }

        private void ProxyManagerPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Activate();
        }

        private void ProxyManagerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Deactivate();
        }
    }
}
