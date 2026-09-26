using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Lanw.Chat.Connection;
using Lanw.Chat.Entities;
using Lanw.Chat.Entities.Nirvana;
using Lanw.Chat.Utils;
using Serilog;

namespace Lanw.Chat.Message;

/// <summary>
///     跨服聊天室（IRC）客户端，由 Nirvana.Chat.Message.ChatMessage 移植。
/// </summary>
public static class ChatMessage {
    private static readonly Uri Url = new("ws://110.42.70.32:13423/ws/chat");
    private static EntityChatConfig _config = new();

    private static Timer? _heartbeat; // 心跳定时器
    private static ClientWebSocket _webSocket = new();

    private static readonly List<IGameConnection> GameConnections = [];

    public static async Task StartAsync()
    {
        foreach (var gameConnection in GameConnections) {
            await StartAsync(gameConnection);
        }
    }

    private static async Task Join(IGameConnection gameConnection)
    {
        if (GameConnections.Any(a => a.GameId == gameConnection.GameId && a.NickName == gameConnection.NickName)) {
            return;
        }

        var entity = new EntityChatJoin {
            GameId = gameConnection.GameId,
            NickName = gameConnection.NickName
        };
        GameConnections.Add(gameConnection);
        await SendAsync(entity);
    }

    public static async Task RemoveJoin(IGameConnection gameConnection)
    {
        foreach (var a in GameConnections.Where(a => a.GameId == gameConnection.GameId && a.NickName == gameConnection.NickName)) {
            GameConnections.Remove(a);
            break;
        }

        var entity = new EntityChatJoin {
            Mode = "removeJoin",
            GameId = gameConnection.GameId,
            NickName = gameConnection.NickName
        };
        await SendAsync(entity);
    }

    public static async Task StartAsync(IGameConnection gameConnection)
    {
        if (_webSocket.State != WebSocketState.Open) {
            await _webSocket.ConnectAsync(Url, CancellationToken.None);
            Log.Information("[IRC] 连接成功!");

            try {
                ChatConfig.IsLogin(); // 检查是否登录
                await AuthenticateAsync(); // 认证
            } catch (Exception) {
                await RefreshChatConfigAsync(); // 刷新配置
            }

            _ = Task.Run(() => Initialize(gameConnection));
        }

        if (_heartbeat == null) {
            var interval = TimeSpan.FromMinutes(5);
            _heartbeat = new Timer(OnHeartbeat, null, TimeSpan.Zero, interval);
        }

        await Join(gameConnection);
    }

    private static void OnHeartbeat(object? state)
    {
        try {
            if (_config.Heartbeats.Count == 0 || !ChatConfig.GetBool("chatEnable")) {
                return;
            }

            // 随机取 IrcInfo.Heartbeats
            var heartbeat = _config.GetHeartbeat();
            if (string.IsNullOrEmpty(heartbeat)) {
                return;
            }

            SendGameMessage(heartbeat);
        } catch (Exception e) {
            Log.Error("[IRC] 心跳失败\n{0}", e.Message);
        }
    }

    public static void Shutdown()
    {
        ShutdownAsync().Wait();
    }

    private static async Task ShutdownAsync()
    {
        if (_heartbeat != null) {
            try {
                await _heartbeat.DisposeAsync(); // 停下
                _heartbeat = null;
            } catch (Exception e) {
                Log.Error("[IRC] 关闭心跳失败\n{0}", e.Message);
            }
        }

        try {
            if (_webSocket.State == WebSocketState.Open) {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
            }
        } catch (Exception e) {
            Log.Error("[IRC] 关闭连接失败\n{0}", e.Message);
        }

        _webSocket = new ClientWebSocket();
    }

    private static void Initialize(IGameConnection gameConnection)
    {
        InitializeAsync(gameConnection).Wait();
    }

    private static async Task InitializeAsync(IGameConnection gameConnection)
    {
        var buffer = new byte[1024 * 4]; // 4KB 缓冲区

        while (_webSocket.State == WebSocketState.Open) {
            try {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close) {
                    Log.Error("[IRC] 服务器要求关闭连接。");
                    Shutdown();
                    _ = StartAsync(gameConnection);
                    return;
                }

                if (result.Count == 0) {
                    continue;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var jsonMsg = JsonSerializer.Deserialize<EntityMode>(message);

                switch (jsonMsg?.Mode ?? string.Empty) {
                    case "chatConfig":
                        ProcessChatConfig(message);
                        break;
                    case "auth":
                        ProcessAuth(message);
                        break;
                    case "chat":
                        ProcessChat(message);
                        break;
                }
            } catch (Exception e) {
                Log.Error("[IRC] 接收消息出错\n{0}", e.Message);
            }
        }
    }

    private static void ProcessAuth(string message)
    {
        ChatConfig.Logout();
        var messageObj = JsonSerializer.Deserialize<EntityMessage>(message);
        if (messageObj == null) {
            Log.Error("[IRC] 登录失败");
            return;
        }

        Log.Error("[IRC] 登录失败: {0}", messageObj.Message);
    }

    /// <summary>
    ///     发送认证请求
    /// </summary>
    public static async Task AuthenticateAsync()
    {
        if (_webSocket.State != WebSocketState.Open) {
            return;
        }

        var account = ChatConfig.GetString("account");

        var authMessage = new {
            mode = "auth",
            account,
            token = ChatConfig.GetString("token")
        };

        await SendAsync(authMessage);

        Log.Information("已发送认证请求。账户: {0}", account);
    }

    private static async Task RefreshChatConfigAsync()
    {
        if (_webSocket.State != WebSocketState.Open) {
            return;
        }

        var authMessage = new {
            mode = "refresh"
        };

        await SendAsync(authMessage);
    }

    private static void ProcessChat(string text)
    {
        var message = JsonSerializer.Deserialize<EntityText>(text);
        if (message == null) {
            Log.Error("[IRC] 解析聊天失败");
            return;
        }

        Log.Information("[IRC] 收到聊天消息: {0}", message.Text);
        SendGameMessage(message.Text);
    }

    private static void ProcessChatConfig(string text)
    {
        var message = JsonSerializer.Deserialize<EntityChatConfig>(text);
        if (message == null) {
            Log.Error("[IRC] 解析聊天配置失败");
            return;
        }

        _config = message;
    }

    private static void SendGameMessage(string message)
    {
        foreach (var gameConnection in GameConnections) {
            PacketTools.SendGameMessage(message, gameConnection);
        }
    }

    public static void SendMessage(string message)
    {
        SendMessageAsync(message).Wait();
    }

    private static async Task SendMessageAsync(string message)
    {
        var entityChat = new EntityChat {
            Message = message
        };
        await SendAsync(entityChat);
    }

    private static async Task SendAsync<TValue>(TValue value)
    {
        if (_webSocket.State != WebSocketState.Open) {
            return;
        }

        var jsonMsg = JsonSerializer.Serialize(value);
        var buffer = Encoding.UTF8.GetBytes(jsonMsg);
        await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
