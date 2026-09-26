using Lanw.Chat.Enums;

namespace Lanw.Chat.Manager;

/// <summary>
///     事件通道与版本通道清单（由 Codexus.Development.SDK.Utils.MessageChannels 中被聊天室使用的部分移植）。
/// </summary>
public static class MessageChannels {
    /// <summary>连接生命周期通道。</summary>
    public const string ChannelConnection = "connection";

    /// <summary>登录成功通道。</summary>
    public const string ChannelLoginSuccess = "loginSuccess";

    /// <summary>全部协议版本通道。</summary>
    public static readonly IReadOnlyList<string> AllVersions = [
        nameof(EnumProtocolVersion.V108X),
        nameof(EnumProtocolVersion.V1122),
        nameof(EnumProtocolVersion.V1180),
        nameof(EnumProtocolVersion.V1200),
        nameof(EnumProtocolVersion.V1206)
    ];
}
