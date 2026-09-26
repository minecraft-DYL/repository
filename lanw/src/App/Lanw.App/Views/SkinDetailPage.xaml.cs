using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace Lanw.App.Views
{
    /// <summary>
    /// 皮肤详情页（对应原 Vue skin/SkinDetail.vue）：大图预览 + 皮肤 ID + 作者/发布时间/下载量/点赞数 +
    /// 皮肤介绍 + 应用设置（EntitySkinSettings，写入单机/网络服/租赁服/本地联机/大厅）。
    /// 数据来自进程内直调的 EntitySkinDetail / NPFLauncher.SetSkinAsync，无 HTTP 中转；
    /// 无网络或皮肤 ID 非法时显示错误态并在页内重试，不崩溃。
    /// 主题跟随系统（ThemeService.FollowSystemTheme）。
    /// </summary>
    public sealed partial class SkinDetailPage : Page
    {
        public SkinDetailViewModel ViewModel { get; } = new();

        public SkinDetailPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
        }

        /// <summary>导航参数为皮肤 ID（entity_id），进入页面即加载详情。</summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var skinId = e.Parameter as string;
            if (ViewModel.LoadCommand.CanExecute(skinId))
            {
                ViewModel.LoadCommand.Execute(skinId);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                return;
            }

            // 直接进入详情页（无历史）时退回皮肤列表
            Frame.Navigate(typeof(SkinsPage));
        }
    }
}
