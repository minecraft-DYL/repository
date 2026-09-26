using Lanw.App.Services;
using Lanw.App.ViewModels;

namespace Lanw.App.Views
{
    /// <summary>
    /// 服务器页（对应原 Vue netgame/Servers.vue）：网络服列表 + 搜索 + 加载更多，点击卡片进详情页。
    /// 数据来自进程内直调的网络服协议（ServerService → Lanw.Public.ServersGameMessage），无 HTTP 中转。
    /// 无网络时由 ViewModel 切换到错误态（页内提示 + 重试），不崩溃。
    /// 主题跟随系统（ThemeService.FollowSystemTheme）。
    /// </summary>
    public sealed partial class ServersPage : Page
    {
        public ServersViewModel ViewModel { get; } = new();

        public ServersPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 进入页面即拉取列表（fire-and-forget，状态由 VM 维护）
            ViewModel.LoadCommand.Execute(null);
        }

        private void ServerList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not ServerItemViewModel item || string.IsNullOrWhiteSpace(item.EntityId))
            {
                return;
            }

            // 详情页挂在同一导航 Frame 上，可返回列表
            Frame.Navigate(typeof(ServerDetailPage), item.EntityId);
        }
    }
}
