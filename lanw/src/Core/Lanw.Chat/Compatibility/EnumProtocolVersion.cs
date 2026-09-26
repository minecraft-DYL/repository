namespace Lanw.Chat.Enums;

/// <summary>
///     Minecraft 协议版本（由 Nirvana.DevPlugin.Enums.EnumProtocolVersion 移植，枚举值保持原样）。
///     本地兼容层类型：Lanw.Development / Lanw.DevPlugin 落地后可整体替换为跨工程共享契约。
/// </summary>
public enum EnumProtocolVersion {
    None = 0, // 无法确定
    All = -1, // 所有版本
    V1076 = 5, // 1.7.6
    V108X = 47, // 1.8.x
    V1122 = 340, // 1.12.2
    V1165 = 754, // 1.16.5
    V1180 = 757, // 1.18.0
    V1200 = 763, // 1.20.0
    V1206 = 766, // 1.20.6
    V1210 = 767 // 1.21.0
}
