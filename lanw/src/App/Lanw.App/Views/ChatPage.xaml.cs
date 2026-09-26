using System.Collections.Specialized;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lanw.App.Views
{
    /// <summary>
    /// 聊天页（对应 README 的「用户聊天室 / IRC 跨服聊天」）：
    /// 消息列表（聊天气泡，§x 颜色码经 MinecraftColorCodeConverter 转色）+ 消息输入/发送 + 聊天室开关。
    /// 数据链路：进程内直调 t19 移植的 Lanw.Chat（ChatMessage / PacketTools / MinecraftColorCodeConverter）
    /// 与 Lanw.Public 的 NirvanaAccountManager.SetChatEnable，无 HTTP。主题跟随系统。
    /// </summary>
    public sealed partial class ChatPage : Page
    {
        public ChatViewModel ViewModel { get; } = new();

        public ChatPage()
        {
            InitializeComponent();
            Services.ThemeService.FollowSystemTheme(this);

            Loaded += ChatPage_Loaded;
            Unloaded += ChatPage_Unloaded;

            // 新消息到达时自动滚动到底部
            ViewModel.Messages.CollectionChanged += Messages_CollectionChanged;
        }

        private void ChatPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Activate();
            ScrollToLatest();
        }

        private void ChatPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Deactivate();
            ViewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                ScrollToLatest();
            }
        }

        private void ScrollToLatest()
        {
            if (ViewModel.Messages.Count == 0)
            {
                return;
            }

            try
            {
                MessageList.ScrollIntoView(ViewModel.Messages[^1]);
            }
            catch (Exception)
            {
                // 忽略：列表尚未完成布局时滚动失败不影响功能
            }
        }

        /// <summary>回车发送（Shift+Enter 换行由 TextBox 自行处理）。</summary>
        private void ChatInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;
            if (ViewModel.SendCommand.CanExecute(null))
            {
                ViewModel.SendCommand.Execute(null);
            }
        }
    }
}
