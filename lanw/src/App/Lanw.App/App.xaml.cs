using Lanw.App.Services;
using Lanw.Core;
using Lanw.Core.Utils;
using Lanw.Public.Utils;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Navigation;

namespace Lanw.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window window = Window.Current;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            // 应用级主题：默认即跟随系统，无需显式设置 RequestedTheme。
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window ??= new Window();
            window.ExtendsContentIntoTitleBar = false;

            // 窗口：按用户要求取消“启动即最大化 / 全屏”，使用普通可还原窗口（1200x800 居中）
            var appWindow = window.AppWindow;
            const int windowWidth = 1200;
            const int windowHeight = 800;
            var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                workArea.X + Math.Max(0, (workArea.Width - windowWidth) / 2),
                workArea.Y + Math.Max(0, (workArea.Height - windowHeight) / 2),
                Math.Min(windowWidth, workArea.Width),
                Math.Min(windowHeight, workArea.Height)));

            if (window.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                window.Content = rootFrame;
            }

            // 主题跟随系统（MachineTheme 亮/暗自适应）
            ThemeService.FollowSystemTheme(rootFrame);

            // 初始化本地配置：确保 resources 目录存在，并加载已持久化的设置（resources/nirvanaAccount.json）。
            // 必须在任何页面读取 LanwConfig 之前执行，否则重启后设置会回落到默认值。
            Directory.CreateDirectory(PathUtil.ResourcePath);
            LanwConfig.Initialization();

            // 内存日志接入：把 t17 移植的 InMemorySink 挂到 Serilog 全局 Logger 上（幂等），
            // 使「日志」页能实时看到各后端模块（Public/Launcher/Chat…）的 Log.* 输出。
            // 必须先于启动期初始化：否则启动阶段的日志（版本检测/插件初始化/缓存预热）进不了「日志」页。
            ViewLogService.Attach();

            // 启动期初始化（对应源 InitProgram.NelInit1，顺序与源一致）：
            // 版本安全检测 → crc_salt 注入 → 插件管理器初始化 → 在线检测 → 后台缓存预热 / 提前获取验证服务器。
            // 命令行实参用于 crc_salt 与 --authenticated_false 开关。
            InitProgram.NelInit1(Environment.GetCommandLineArgs());

            // 主壳：NavigationView 导航框架（启动落点为账号管理页）
            _ = rootFrame.Navigate(typeof(ShellPage), e.Arguments);
            window.Activate();

            // 启动时自动登录上次活跃账号（后台执行，不阻塞 UI；失败静默丢弃，状态在各页 GetCurrent 展示）
            _ = AutoLoginOnStartupAsync();
        }

        /// <summary>后台自动登录上次活跃账号。</summary>
        private static async Task AutoLoginOnStartupAsync()
        {
            try
            {
                await new AuthService().AutoLoginLastActiveAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 登录失败静默，不阻断应用启动。
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}