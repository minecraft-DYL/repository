using Lanw.App.Services;
using Lanw.App.ViewModels;

namespace Lanw.App.Views
{
    /// <summary>
    /// 版本页（对应原版 /api/version 与 others/Version.vue）：
    /// 展示当前版本号（LanwProgram.Version/VersionId/Mode/Arch）并可检查更新
    /// （只读读取 t17 移植的更新清单，不会下载文件或退出进程）。
    /// </summary>
    public sealed partial class VersionPage : Page
    {
        public VersionViewModel ViewModel { get; } = new();

        public VersionPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
        }
    }
}
