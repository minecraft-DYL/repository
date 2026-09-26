using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Lanw.App.Views
{
    /// <summary>
    /// 服务器详情页（对应原 Vue netgame/ServerDetail.vue 的信息区）：
    /// 消息/信息列表（服务器 ID、作者、创建时间、游戏版本、服务器地址+一键复制）、主图 + 小图切换、服务器介绍，
    /// 以及按需求新增的「服务器 MOD」区（该服 JAVA 游戏版本 + MOD 清单 + 导出 ZIP）。
    /// 数据来自进程内直调的 EntityServerDetail（并行拉详情 + 地址），无 HTTP 中转；无网络时显示错误态并在页内重试。
    /// 需求变更：原 Vue 的 Launch / 启动代理弹窗（启动游戏）不再移植。
    /// 主题跟随系统（ThemeService.FollowSystemTheme）。
    /// </summary>
    public sealed partial class ServerDetailPage : Page
    {
        public ServerDetailViewModel ViewModel { get; } = new();

        public ServerDetailPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);

            // 「导出 MOD 为 ZIP」需要宿主窗口句柄弹保存位置选择器：XamlRoot 在点击时才可用，故延迟解析
            ViewModel.SaveZipPrompt = suggested => ModsZipExporter.PickZipPathAsync(this.XamlRoot, suggested);
        }

        /// <summary>导航参数为服务器 ID（entity_id），进入页面即加载详情。</summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var serverId = e.Parameter as string;
            if (ViewModel.LoadCommand.CanExecute(serverId))
            {
                ViewModel.LoadCommand.Execute(serverId);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                return;
            }

            // 直接进入详情页（无历史）时退回服务器列表
            Frame.Navigate(typeof(ServersPage));
        }

        private void Thumbnails_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.ShowImage(e.ClickedItem as ServerImageItemViewModel);
        }
    }
}
