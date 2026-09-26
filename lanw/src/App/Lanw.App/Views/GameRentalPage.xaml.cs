using Lanw.App.Services;
using Lanw.App.ViewModels;

namespace Lanw.App.Views
{
    /// <summary>
    /// 租赁服页（对应原 Vue rental/GameRental.vue）：租赁服列表（名称 / 在线状态 EnumServerStatus /
    /// 版本 / 可见性 EnumVisibilityStatus / 在线人数）+ 搜索 + 加载更多，点击卡片进详情页。
    /// 数据来自进程内直调的租赁服协议（RentalGameService → Lanw.Public.RentalGameMessage +
    /// Lanw.WPFLauncher 租赁服端点），无 HTTP 中转。
    /// 无网络时由 ViewModel 切换到错误态（页内提示 + 重试），不崩溃。
    /// 主题跟随系统（ThemeService.FollowSystemTheme）。
    /// </summary>
    public sealed partial class GameRentalPage : Page
    {
        public GameRentalViewModel ViewModel { get; } = new();

        public GameRentalPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 进入页面即拉取列表（fire-and-forget，状态由 VM 维护）
            ViewModel.LoadCommand.Execute(null);
        }

        private void RentalList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not RentalGameItemViewModel item || string.IsNullOrWhiteSpace(item.EntityId))
            {
                return;
            }

            // 详情页挂在同一导航 Frame 上，可返回列表
            Frame.Navigate(typeof(GameRentalDetailPage), item.EntityId);
        }
    }
}
