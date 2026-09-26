using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Lanw.Chat.Enums;

namespace Lanw.App.ViewModels;

/// <summary>
/// 聊天页视图模型（对应 README 的「用户聊天室 / IRC 跨服聊天」）。
///
/// 功能：
/// 1) 消息列表：本机发送回显 + 连接/开关等状态条目，文本经 Lanw.Chat 的 MinecraftColorCodeConverter
///    解析 §x 颜色码后按段着色（气泡样式）；
/// 2) 消息输入 + 发送：进程内直调 <c>Lanw.Chat.Message.ChatMessage.SendMessage</c>（按 join 时的协议版本组包）；
/// 3) 聊天室开关：读写 <c>chatEnable</c> 全局配置（<c>NirvanaAccountManager.SetChatEnable</c> 联动），
///    该开关影响游戏内 <c>/irc</c> 指令拦截与聊天室心跳；
/// 4) 进入/离开聊天室：<c>ChatMessage.StartAsync(IGameConnection)</c> / <c>Shutdown</c>，全部经 ChatService 兜底，
///    无网络时只落到「连接失败」状态，不抛出、不崩溃。
/// 无 HTTP：所有调用都在进程内完成。
/// </summary>
public sealed partial class ChatViewModel : ObservableObject
{
    private readonly AccountRepository _accounts = new();
    private readonly ChatService _chat = ChatService.Current;

    private bool _activated;

    // 构造期抑制开关落盘（初始值回填不应写配置文件）
    private bool _loading = true;

    /// <summary>聊天室开关（chatEnable 全局配置）。</summary>
    [ObservableProperty]
    public partial bool ChatEnabled { get; set; }

    /// <summary>聊天昵称（IRC join 用；默认取当前账号）。</summary>
    [ObservableProperty]
    public partial string NickName { get; set; }

    /// <summary>游戏/服务器标识（IRC join 用，缺省 "-1"）。</summary>
    [ObservableProperty]
    public partial string GameId { get; set; }

    /// <summary>协议版本下拉选中项（0=1.8.9 1=1.12.2 2=1.18.1 3=1.20.1）。</summary>
    [ObservableProperty]
    public partial int ProtocolIndex { get; set; }

    /// <summary>输入框文本。</summary>
    [ObservableProperty]
    public partial string InputText { get; set; }

    /// <summary>状态提示。</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    /// <summary>连接状态展示。</summary>
    [ObservableProperty]
    public partial string ConnectionStateText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    public partial bool IsConnecting { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    /// <summary>连接按钮可用性（连接中禁用，避免重复点击）。</summary>
    public bool CanConnect => !IsConnecting;

    /// <summary>当前账号展示（昵称来源提示）。</summary>
    [ObservableProperty]
    public partial string CurrentAccountDisplay { get; set; }

    /// <summary>连接按钮文案（已在聊天室时可重新进入）。</summary>
    public string ConnectButtonText => IsConnected ? "重新进入聊天室" : "进入聊天室";

    /// <summary>消息列表（本机会话记录：发送回显 + 状态条目）。</summary>
    public ObservableCollection<ChatMessageItemViewModel> Messages { get; } = [];

    public ChatViewModel()
    {
        // 先给出非空初值，避免可空引用告警
        NickName = string.Empty;
        GameId = "-1";
        InputText = string.Empty;
        StatusMessage = string.Empty;
        ConnectionStateText = string.Empty;
        CurrentAccountDisplay = string.Empty;

        ProtocolIndex = 3; // 缺省 1.20.1
        ChatEnabled = _chat.ChatEnabled;

        RefreshAccount();
        RefreshState();

        Append(ChatMessageItemViewModel.System(
            "§a欢迎使用 lanw 用户聊天室§r（IRC 跨服聊天）。下行消息由 Lanw.Chat 直接写回游戏客户端（游戏内聊天框可见）；" +
            "本页记录本机发送回显与连接状态。"));

        _loading = false;
    }

    /// <summary>页面进入：订阅连接状态变化。</summary>
    public void Activate()
    {
        if (!_activated)
        {
            _chat.Changed += OnChatServiceChanged;
            _activated = true;
        }

        RefreshAccount();
        RefreshState();
    }

    /// <summary>页面离开：退订。</summary>
    public void Deactivate()
    {
        if (_activated)
        {
            _chat.Changed -= OnChatServiceChanged;
            _activated = false;
        }
    }

    /// <summary>进入聊天室（无网络/服务器不可达时只落到失败状态）。</summary>
    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnecting)
        {
            return;
        }

        var nick = (NickName ?? string.Empty).Trim();
        if (nick.Length == 0)
        {
            StatusMessage = "请先填写聊天昵称：可点「用当前账号」自动填入，或先在「登录」页登录账号";
            return;
        }

        IsConnecting = true;
        var protocol = ProtocolOf(ProtocolIndex);
        Append(ChatMessageItemViewModel.System($"正在连接聊天室服务器（昵称 {nick}，协议 {ChatService.DescribeProtocol(protocol)}）…"));

        try
        {
            var ok = await _chat.ConnectAsync(nick, GameId, protocol);
            RefreshState();
            if (ok)
            {
                StatusMessage = _chat.StateMessage;
                Append(ChatMessageItemViewModel.System(_chat.StateMessage));
            }
            else
            {
                StatusMessage = _chat.StateMessage;
                Append(ChatMessageItemViewModel.Error(_chat.StateMessage));
            }
        }
        finally
        {
            IsConnecting = false;
            RefreshState();
        }
    }

    /// <summary>离开聊天室。</summary>
    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            await _chat.DisconnectAsync();
        }
        finally
        {
            RefreshState();
            StatusMessage = _chat.StateMessage;
            Append(ChatMessageItemViewModel.System(_chat.StateMessage));
        }
    }

    /// <summary>发送消息（未连接时给出提示，不抛出）。</summary>
    [RelayCommand]
    private async Task SendAsync()
    {
        var text = (InputText ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            StatusMessage = "请输入要发送的消息";
            return;
        }

        if (!_chat.IsConnected)
        {
            StatusMessage = "未连接聊天室服务器：请先点击「进入聊天室」（无网络时无法连接）";
            Append(ChatMessageItemViewModel.Error("发送失败：尚未连接聊天室服务器"));
            return;
        }

        var ok = await _chat.SendAsync(text);
        if (ok)
        {
            InputText = string.Empty;
            var sender = (NickName ?? string.Empty).Trim();
            Append(ChatMessageItemViewModel.Sent(text, sender.Length == 0 ? "我" : sender));
            StatusMessage = "已发送";
        }
        else
        {
            StatusMessage = _chat.StateMessage;
            Append(ChatMessageItemViewModel.Error(_chat.StateMessage));
        }
    }

    /// <summary>清空本机会话记录。</summary>
    [RelayCommand]
    private void ClearMessages()
    {
        Messages.Clear();
        Append(ChatMessageItemViewModel.System("已清空本机会话记录"));
        StatusMessage = "已清空本机会话记录";
    }

    /// <summary>用当前账号名填充聊天昵称。</summary>
    [RelayCommand]
    private void UseCurrentAccount()
    {
        var name = CurrentAccountName();
        if (name.Length == 0)
        {
            StatusMessage = "当前没有已登录账号：请先在「登录」页登录";
            return;
        }

        NickName = name;
        StatusMessage = $"已填入当前账号昵称：{name}";
    }

    partial void OnChatEnabledChanged(bool value)
    {
        if (_loading)
        {
            return; // 构造期回填，不写盘
        }

        _chat.SetChatEnabled(value);
        StatusMessage = value
            ? "已开启聊天室（游戏内 /irc 指令拦截与心跳生效）"
            : "已关闭聊天室（游戏内 /irc 指令与心跳停用）";
        Append(ChatMessageItemViewModel.System(StatusMessage));
    }

    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectButtonText));

    private void OnChatServiceChanged(object? sender, EventArgs e) => RefreshState();

    private void RefreshState()
    {
        var state = _chat.State;
        IsConnected = state == ChatLinkState.Connected;
        IsConnecting = state == ChatLinkState.Connecting;
        ConnectionStateText = state switch
        {
            ChatLinkState.Connected => $"已连接 · {_chat.StateMessage}",
            ChatLinkState.Connecting => "正在连接…",
            ChatLinkState.Failed => $"连接失败 · {_chat.StateMessage}",
            _ => $"未连接 · {_chat.StateMessage}",
        };
    }

    private void RefreshAccount()
    {
        var name = CurrentAccountName();
        CurrentAccountDisplay = name.Length == 0
            ? "当前账号：未登录（可手动填写聊天昵称）"
            : $"当前账号：{name}";

        if ((NickName ?? string.Empty).Trim().Length == 0 && name.Length > 0)
        {
            // 仅在昵称仍为空时填入当前账号名，不覆盖用户输入
            NickName = name;
        }
    }

    private string CurrentAccountName()
    {
        try
        {
            var account = _accounts.GetCurrent();
            if (account == null)
            {
                return string.Empty;
            }

            foreach (var candidate in new[] { account.Name, account.Account, account.UserId })
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }
        }
        catch (Exception)
        {
            // 忽略：视为未登录
        }

        return string.Empty;
    }

    /// <summary>下拉索引 → 协议版本（与 Lanw.Chat 的 EnumProtocolVersion 对应）。</summary>
    private static EnumProtocolVersion ProtocolOf(int index) => index switch
    {
        0 => EnumProtocolVersion.V108X,
        1 => EnumProtocolVersion.V1122,
        2 => EnumProtocolVersion.V1180,
        _ => EnumProtocolVersion.V1200,
    };

    private void Append(ChatMessageItemViewModel item)
    {
        Messages.Add(item);
        while (Messages.Count > MaxMessages)
        {
            Messages.RemoveAt(0); // 只保留最近若干条，避免长时间运行内存增长
        }
    }

    private const int MaxMessages = 500;
}
