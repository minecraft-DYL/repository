using System.Net;
using System.Net.Sockets;
using System.Text;
using Lanw.DevPlugin.Entities;
using Serilog;

namespace Lanw.Development.Utils;

public class UdpBroadcaster {
    // 原实现使用 Nirvana.Game.Launcher.Utils.GameVersionUtil + Nirvana.WPFLauncher.Utils.EnumGameVersion
    // （Lanw.Game.Launcher 尚未移植，见任务 t18），此处内联等价映射，落地后改回引用。
    private static readonly Dictionary<string, uint> GameVersionDictEx = new() {
        { "", 0 },
        { "1.7.10", 1007010 },
        { "1.10.2", 1010002 },
        { "1.8", 1008000 },
        { "1.11.2", 1011002 },
        { "1.12", 1012000 },
        { "1.12.2", 1012002 },
        { "1.8.8", 1008008 },
        { "1.8.9", 1008009 },
        { "1.9.4", 1009004 },
        { "1.6.4", 1006004 },
        { "1.7.2", 1007002 },
        { "1.18", 1018000 },
        { "1.20", 1020000 },
        { "1.21", 1021000 },
        { "1.21.8", 1021008 }
    };

    private const uint Version189 = 1008009;

    private readonly InterceptorConfig _config;

    private readonly int _serverPort;

    private readonly IPEndPoint _targetEndPoint;

    private readonly UdpClient _udpClient;

    private bool _running;

    public UdpBroadcaster(int targetPort, InterceptorConfig config, string multicastAddress = "224.0.2.60", int port = 4445)
    {
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _targetEndPoint = new IPEndPoint(IPAddress.Parse(multicastAddress), port);
        _udpClient.MulticastLoopback = true;
        _serverPort = targetPort;
        _config = config;
    }


    public async Task StartBroadcastingAsync()
    {
        _running = true;
        try {
            while (_running) {
                await SendMessageAsync();
                await Task.Delay(2000);
            }
        } catch (OperationCanceledException ex) {
            Log.Error("Broadcasting operation cancelled, {0}", ex.Message);
        } catch (Exception value) {
            Log.Error("UDP Broadcast error: {0}", value);
        }
    }

    private async Task SendMessageAsync()
    {
        try {
            var message = BuildMessage();
            var bytes = Encoding.UTF8.GetBytes(message);
            await _udpClient.SendAsync(bytes, bytes.Length, _targetEndPoint);
        } catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostUnreachable) {
            await Task.Delay(5000);
        } catch (Exception value) {
            Log.Error("UDP Send failed: {0}", value);
        }
    }

    private string BuildMessage()
    {
        var gameVersion = GameVersionDictEx.GetValueOrDefault(_config.ServerVersion, 0u);
        return gameVersion > Version189 ? $"[MOTD] §cNirvana §f{_config.ServerName} -> {_config.NickName}[/MOTD][AD]{_serverPort}[/AD]" : $"[MOTD] Nirvana {_config.ServerName} -> {_config.NickName}[/MOTD][AD]{_serverPort}[/AD]";
    }

    public void Stop()
    {
        _running = false;
    }
}
