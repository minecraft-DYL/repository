using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Lanw.App.Views
{
    /// <summary>
    /// 租赁服详情页（对应原 Vue rental/GameRentalDetail.vue 的信息区）：
    /// 服务器信息（EntityRentalGameDetails）、服务器地址（EntityRentalGameServerAddress，含移动/电信/联通三线接入 + 复制地址）、
    /// 玩家列表（EntityRentalGamePlayerList）、服务器介绍。
    /// 数据来自进程内直调的租赁服协议（RentalGameService → Lanw.WPFLauncher 租赁服端点），无 HTTP 中转；
    /// 详情失败进入错误态并可在页内重试，地址/玩家列表失败只做降级提示，不崩溃。
    /// 注：原 Vue 的 Launch / 启动代理弹窗与「添加名称」（创建租赁服游戏角色）属于启动流程，由后续任务移植，本页不涉及。
    /// 主题跟随系统（ThemeService.FollowSystemTheme）。
    /// </summary>
    public sealed partial class GameRentalDetailPage : Page
    {
        public GameRentalDetailViewModel ViewModel { get; } = new();

        public GameRentalDetailPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
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

            // 直接进入详情页（无历史）时退回租赁服列表
            Frame.Navigate(typeof(GameRentalPage));
        }
    }
}
