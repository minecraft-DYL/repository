using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Lanw.Cipher.Cipher;
using Lanw.Cipher.Entities.Yggdrasil;
using Lanw.Cipher.Extensions;
using Lanw.Cipher.Generator;
using Lanw.Cipher.Protocol;
using Serilog;

namespace Lanw.Cipher.Yggdrasil;

/// <summary>
/// 网易标准 Yggdrasil 认证交换（TCP 连接网易认证服务器）（由 Nirvana.Cipher.Yggdrasil.StandardYggdrasil 移植，自研）。
/// 认证服务器列表来自 X19Extensions.UpdateNetease 接口。
/// </summary>
public static class StandardYggdrasil
{
    private static readonly byte[] ChaChaNonce = "163 NetEase\n"u8.ToArray();

    private static YggdrasilServer[]? _address;

    public static async Task InitializationAsync()
    {
        _address = await RandomAuthServer();
    }

    public static async Task JoinServerAsync(GameProfile profile, string serverId, bool login = false)
    {
        if (_address == null)
        {
            throw new Exception("Not StandardYggdrasil Servers Found.");
        }

        using var client = new TcpClient();

        try
        {
            var random = new Random();
            var server = _address[random.Next(_address.Length)];

            Log.Information("StandardYggdrasil: {0}:{1}", server.Ip, server.Port);
            await client.ConnectAsync(server.Ip, server.Port);

            if (!client.Connected)
            {
                throw new TimeoutException($"Connecting to server {server.Ip}:{server.Port} timed out");
            }

            var stream = client.GetStream();
            var initiated = await InitializeConnection(stream, profile);

            if (login)
            {
                return;
            }

            await MakeRequest(stream, profile, serverId, initiated);
        }
        catch (SocketException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Unexpected error: {ex.Message}");
        }
    }

    private static async Task<byte[]> InitializeConnection(NetworkStream stream, GameProfile profile)
    {
        using var receive = await stream.ReadSteamWithInt16Async();

        if (receive.Length < 272)
        {
            throw new Exception("Invalid Response Length");
        }

        var loginSeed = new byte[16];
        var signContent = new byte[256];
        receive.ReadExactly(loginSeed);
        receive.ReadExactly(signContent);

        var message = YggdrasilGenerator.GenerateInitializeMessage(profile, loginSeed, signContent);
        await stream.WriteAsync(message);

        using var response = await stream.ReadSteamWithInt16Async();

        if (response.Length < 1)
        {
            throw new Exception("Empty Response");
        }

        var status = response.ReadByte();

        return status != 0 ? throw new Exception($"Initialization failed with status: 0x{status:X2}") : loginSeed;
    }

    private static async Task MakeRequest(NetworkStream stream, GameProfile profile, string serverId, byte[] loginSeed)
    {
        var token = profile.User.GetAuthToken();

        var packer = new ChaChaPacker(token.CombineWith(loginSeed), ChaChaNonce, true);
        var unpacker = new ChaChaPacker(loginSeed.CombineWith(token), ChaChaNonce, false);
        var message = packer.PackMessage(9, YggdrasilGenerator.GenerateJoinMessage(profile, serverId, loginSeed));
        await stream.WriteAsync(message);

        using var messageStream = await stream.ReadSteamWithInt16Async();
        var packMessage = messageStream.ToArray();
        var (type, unpackMessage) = unpacker.UnpackMessage(packMessage);
        if (type != 9 || unpackMessage[0] != 0x00)
        {
            throw new Exception(Convert.ToHexString([unpackMessage[0]]));
        }
    }

    private static async Task<YggdrasilServer[]> RandomAuthServer()
    {
        var servers = await NeteaseApi.UpdateNetease.ApiAsync<YggdrasilServer[]>("/authserver.list");

        if (servers == null || servers.Length == 0)
        {
            throw new Exception("Not StandardYggdrasil Servers Found.");
        }

        return servers;
    }
}