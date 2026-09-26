using DotNetty.Transport.Channels;
using Lanw.Chat.Connection;
using Lanw.Chat.Enums;
using Lanw.Chat.Message;
using Lanw.Core;
using Lanw.Public.Manager;
using Serilog;

namespace Lanw.App.Services;

/// <summary>聊天室连接状态。</summary>
public enum ChatLinkState
{
    /// <summary>未连接。</summary>
    Disconnected,

    /// <summary>正在连接。</summary>
    Connecting,

    /// <summary>已连接。</summary>
    Connected,

    /// <summary>连接失败（无网络/服务器不可达/被服务端拒绝）。</summary>
    Failed,
}

/// <summary>
/// 聊天室服务：进程内直调 t19 移植的 <c>Lanw.Chat</c>（ChatMessage / PacketTools / MinecraftColorCodeConverter），无 HTTP。
///
/// 职责：
/// 1) 建立/断开 IRC 连接 —— <see cref="ChatMessage.StartAsync(IGameConnection)" /> / <see cref="ChatMessage.Shutdown" />；
/// 2) 发送本机输入 —— <see cref="ChatMessage.SendMessage" />（内部按 join 时的协议版本组包，写回游戏客户端）；
/// 3) 聊天室开关 —— 读写 <c>chatEnable</c> 全局配置（<see cref="NirvanaAccountManager.SetChatEnable" /> 联动），
///    该开关同时被 Lanw.Chat 的 Packet.CommandBase（游戏内 <c>/irc</c> 指令拦截）与 ChatMessage 心跳读取；
/// 4) 读取 <c>EntityChatConfig</c> 下发的聊天室配置（心跳/在线玩家）—— Lanw.Chat 在收到配置后存于内部字段，
///    未对外暴露读取接口，故本服务只按「聊天室开关 + 连接状态」反映其状态。
///
/// 说明（下行消息）：Lanw.Chat 收到服务端聊天消息后直接经 <c>PacketTools.SendGameMessage</c> 写回游戏客户端，
/// 不向宿主暴露接收事件；聊天页不接管游戏连接（ClientChannel 由代理/启动模块的 GameConnection 提供），
/// 因此本页的「消息列表」是本机会话记录（发送回显 + 状态条目），下行消息在游戏内聊天框显示。
/// </summary>
public sealed class ChatService
{
    /// <summary>进程内单例（与 ProxyService.Current 一致的服务形态）。</summary>
    public static ChatService Current { get; } = new();

    private ChatService()
    {
    }

    /// <summary>连接状态变化（UI 订阅后刷新状态区；在 UI 线程触发）。</summary>
    public event EventHandler? Changed;

    /// <summary>当前连接状态。</summary>
    public ChatLinkState State { get; private set; } = ChatLinkState.Disconnected;

    /// <summary>状态说明（连接结果/错误原因，供 UI 直接展示）。</summary>
    public string StateMessage { get; private set; } = "未连接聊天室服务器";

    /// <summary>最后一次尝试连接使用的昵称（重连时回填）。</summary>
    public string LastNickName { get; private set; } = string.Empty;

    /// <summary>最后一次尝试连接使用的协议版本。</summary>
    public EnumProtocolVersion LastProtocolVersion { get; private set; } = EnumProtocolVersion.V1200;

    /// <summary>是否已连接（发送按钮的可用条件）。</summary>
    public bool IsConnected => State == ChatLinkState.Connected;

    /// <summary>聊天室开关（chatEnable 全局配置，缺省 true，与 LanwConfig 注册值一致）。</summary>
    public bool ChatEnabled
    {
        get
        {
            try
            {
                return LanwConfig.GetValue<bool>("chatEnable");
            }
            catch (Exception)
            {
                return true; // 配置未初始化时按缺省开启，避免开关状态误判
            }
        }
    }

    /// <summary>写聊天室开关（落盘 resources/nirvanaAccount.json，与参考实现一致）。</summary>
    public void SetChatEnabled(bool enabled)
    {
        try
        {
            NirvanaAccountManager.SetChatEnable(enabled ? "true" : "false");
        }
        catch (Exception ex)
        {
            Log.Error("[IRC] 写入聊天室开关失败\n{0}", ex.Message);
        }
    }

    /// <summary>
    /// 进入聊天室：按昵称/游戏ID/协议版本建立连接（无网络或服务器不可达时返回 false，不抛出）。
    /// </summary>
    public async Task<bool> ConnectAsync(string nickName, string gameId, EnumProtocolVersion protocolVersion)
    {
        if (State == ChatLinkState.Connecting)
        {
            return false;
        }

        if (State == ChatLinkState.Connected)
        {
            SetState(ChatLinkState.Connected, $"已在聊天室中（昵称 {LastNickName}）");
            return true;
        }

        SetState(ChatLinkState.Connecting, "正在连接聊天室服务器…");

        try
        {
            var connection = new ChatGameConnection(
                string.IsNullOrWhiteSpace(gameId) ? "-1" : gameId.Trim(),
                nickName.Trim(),
                protocolVersion);

            // Lanw.Chat 的 IRC 客户端连接远端 ws，可能因无网络/服务器不可达而抛异常，这里统一兜底成失败状态。
            await Task.Run(() => ChatMessage.StartAsync(connection)).ConfigureAwait(true);

            LastNickName = nickName.Trim();
            LastProtocolVersion = protocolVersion;
            SetState(
                ChatLinkState.Connected,
                $"已连接聊天室（昵称 {LastNickName}，协议 {DescribeProtocol(protocolVersion)}）");
            Log.Information("[IRC] 聊天页已进入聊天室：{0}", LastNickName);
            return true;
        }
        catch (Exception ex)
        {
            SetState(
                ChatLinkState.Failed,
                $"连接失败：{ex.Message}（无网络或聊天室服务器不可达时可稍后重试，不影响其它功能）");
            return false;
        }
    }

    /// <summary>
    /// 离开聊天室：关闭心跳与 ws 连接（失败只记录，不影响 UI）。
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (State == ChatLinkState.Disconnected)
        {
            SetState(ChatLinkState.Disconnected, "未连接聊天室服务器");
            return;
        }

        try
        {
            // Shutdown 内部同步 Wait，放到线程池执行，避免阻塞 UI 线程。
            await Task.Run(ChatMessage.Shutdown).ConfigureAwait(true);
            SetState(ChatLinkState.Disconnected, "已离开聊天室");
        }
        catch (Exception ex)
        {
            SetState(ChatLinkState.Disconnected, $"已离开聊天室（关闭连接时出现异常：{ex.Message}）");
        }
    }

    /// <summary>
    /// 发送一条聊天室消息。未连接时返回 false 并给出提示（不抛出）。
    /// </summary>
    public async Task<bool> SendAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (State != ChatLinkState.Connected)
        {
            SetState(State, "未连接聊天室服务器：请先点击「进入聊天室」（无网络时无法连接）");
            return false;
        }

        try
        {
            // Lanw.Chat 在 socket 非 Open 时会静默丢弃，这里已按自身记录的连接状态先行拦截。
            await Task.Run(() => ChatMessage.SendMessage(message)).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            SetState(ChatLinkState.Connected, $"发送失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>协议版本展示名（1.8.x / 1.12.2 / 1.18.0 / 1.20.0）。</summary>
    public static string DescribeProtocol(EnumProtocolVersion protocolVersion)
    {
        return protocolVersion switch
        {
            EnumProtocolVersion.V108X => "1.8.x",
            EnumProtocolVersion.V1122 => "1.12.2",
            EnumProtocolVersion.V1180 => "1.18.1",
            EnumProtocolVersion.V1200 => "1.20.1",
            _ => protocolVersion.ToString(),
        };
    }

    private void SetState(ChatLinkState state, string message)
    {
        State = state;
        StateMessage = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 聊天页使用的游戏连接契约实现（Lanw.Chat.Connection.IGameConnection）。
    /// 代理层 GameConnection 落地后可替换为真实连接；聊天页仅需要 游戏标识 + 昵称 + 协议版本 三个字段。
    /// </summary>
    private sealed class ChatGameConnection(string gameId, string nickName, EnumProtocolVersion protocolVersion) : IGameConnection
    {
        public string GameId { get; } = gameId;

        public string NickName { get; } = nickName;

        public EnumProtocolVersion ProtocolVersion { get; } = protocolVersion;

        /// <summary>
        /// 游戏客户端通道（下行消息写回游戏端）。聊天页不接管游戏连接，故为 null；
        /// PacketTools.SendGameMessage 内部已 try/catch，不会因空通道崩溃（仅记日志）。
        /// </summary>
        public IChannel ClientChannel => null!;
    }
}
