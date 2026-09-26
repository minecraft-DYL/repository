using Lanw.App.Services;
using Lanw.App.ViewModels;

namespace Lanw.App.Views
{
    /// <summary>
    /// 用户中心页（对应原 Vue nirvana/UserHome.vue + nirvana/NirvanaLogin.vue）：
    /// 未登录时本页即涅槃账号登录入口（账号 + 密码 + 登录，登录成功即展示账号信息卡，
    /// 对应原版登录成功后跳转 /user）；展示剩余天数与（可掩码的）账号，
    /// 隐藏账号开关写 LanwConfig.hideAccount，退出登录走 LanwConfig.Logout()；数据进程内直调，无 HTTP。
    /// </summary>
    public sealed partial class UserHomePage : Page
    {
        public UserHomeViewModel ViewModel { get; } = new();

        public UserHomePage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
            Loaded += UserHomePage_Loaded;
        }

        private async void UserHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            // 每次进入页面重新读取（参考源 onMounted）
            await ViewModel.LoadAsync();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // 退出后停留在本页：本页的未登录卡片就是涅槃账号登录入口（对应原版退出后回到登录页的行为）。
            ViewModel.Logout();
        }
    }
}
