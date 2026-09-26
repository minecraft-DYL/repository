using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Dispatching;

namespace Lanw.App.Views
{
    /// <summary>
    /// 日志信息页（对应原 Vue others/Logs.vue + /api/logs）：
    /// 读取 t17 移植的后端内存日志（InMemorySink），每秒定时刷新（实时日志流），按级别着色。
    /// </summary>
    public sealed partial class LogsPage : Page
    {
        private readonly DispatcherQueueTimer _timer;

        public LogsViewModel ViewModel { get; } = new();

        public LogsPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
            ViewModel.ApplyTheme(ActualTheme == ElementTheme.Light);

            _timer = DispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, _) =>
            {
                if (ViewModel.IsAutoRefresh)
                {
                    ViewModel.RefreshFromSink();
                }
            };

            Loaded += LogsPage_Loaded;
            Unloaded += LogsPage_Unloaded;
            ActualThemeChanged += LogsPage_ActualThemeChanged;
        }

        private void LogsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshFromSink();
            _timer.Start();
        }

        private void LogsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private void LogsPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            ViewModel.ApplyTheme(ActualTheme == ElementTheme.Light);
        }
    }
}
