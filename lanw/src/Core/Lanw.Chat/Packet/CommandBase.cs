using DotNetty.Buffers;
using Lanw.Chat.Connection;
using Lanw.Chat.Enums;
using Lanw.Chat.Extensions;
using Lanw.Chat.Message;
using Lanw.Chat.Packet.IPacket;
using Lanw.Chat.Utils;

namespace Lanw.Chat.Packet;

/// <summary>
///     聊天/命令包基类（跨服 IRC 拦截），由 Nirvana.Chat.Packet.CommandBase 移植。
/// </summary>
public class CommandBase : APacket {
    private string _command = string.Empty;
    private bool _isCommand;

    private byte[]? _rawBytes;

    protected CommandBase(EnumConnectionState state, EnumPacketDirection direction, int packetId, EnumProtocolVersion version, bool skip = false) : base(state, direction, packetId, version, skip) { }

    public override void ReadFromBuffer(IByteBuffer buffer)
    {
        _rawBytes = new byte[buffer.ReadableBytes];
        buffer.GetBytes(buffer.ReaderIndex, _rawBytes);

        _command = buffer.ReadStringFromBuffer(short.MaxValue);
        _isCommand = IsIrc(_command);
    }

    public override void WriteToBuffer(IByteBuffer buffer)
    {
        if (_isCommand && ChatConfig.GetBool("chatEnable")) {
            return;
        }

        if (_rawBytes != null) {
            buffer.WriteBytes(_rawBytes);
        }
    }

    public override bool HandlePacket(IGameConnection connection)
    {
        if (!_isCommand || !ChatConfig.GetBool("chatEnable")) {
            return false;
        }

        // "/irc 123" > "123"
        // "irc 123" > "123 " > "123"
        var content = _command.Length > 4 ? _command[4..].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(content)) {
            PacketTools.SendGameMessage("§e[IRC]: 请使用 /irc <消息>", connection);
            return true;
        }

        try {
            ChatConfig.IsLogin(); // 检查是否登录
        } catch (Exception) {
            PacketTools.SendGameMessage("§e[IRC]: 请先登录账号", connection);
            return true;
        }

        ChatMessage.SendMessage(content);
        return true;
    }

    private static bool IsIrc(string command)
    {
        var commandsPrefix = new List<string> {
            "irc", "chat"
        };

        foreach (var prefix in commandsPrefix) {
            // 1.20.1
            if (command.StartsWith($"{prefix} ", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            // 1.20.1
            if (command.Equals($"{prefix}", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            // 1.18.1 | 1.12.2
            if (command.StartsWith($"/{prefix} ", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            // 1.18.1 | 1.12.2
            if (command.Equals($"/{prefix}", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}
