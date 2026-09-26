using Lanw.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lanw.App.Views
{
    /// <summary>
    /// 启动管理页：版本 / 内存 / JVM 参数 + 一键启动（进度与日志）+ 运行中实例管理。
    /// 数据来自进程内 Lanw.Game.Launcher（GameLaunchService），无 HTTP。
    /// 主题跟随系统（根 Frame 使用 ElementTheme.Default）。
    /// </summary>
    public sealed partial class GameLaunchManagerPage : Page
    {
        public GameLaunchManagerViewModel ViewModel { get; } = new();

        public GameLaunchManagerPage()
        {
            this.InitializeComponent();
            Loaded += GameLaunchManagerPage_Loaded;
            Unloaded += GameLaunchManagerPage_Unloaded;
        }

        private void GameLaunchManagerPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Activate();
        }

        private void GameLaunchManagerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Deactivate();
        }

        /// <summary>关闭列表中指定的游戏实例（DataTemplate 内不便直接绑定命令，走代码后置）。</summary>
        private void CloseInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: GameInstanceItemViewModel item })
            {
                ViewModel.CloseInstanceCommand.Execute(item);
            }
        }
    }
}
