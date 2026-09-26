using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lanw.Cipher.Cipher.Nirvana.Connection;
using Lanw.Core.Entities.Login;
using Serilog;

namespace Lanw.Cipher.Cipher.Nirvana.Protocols;

/// <summary>
/// 网易 AuthLib 本地鉴权 Socket 服务端：接收游戏客户端回填的游戏 ID/用户 ID/服务器 ID，
/// 代为完成网易认证（由 Nirvana.Cipher.Cipher.Nirvana.Protocols.AuthLibProtocol 移植，自研）。
/// </summary>
public class AuthLibProtocol(int port, string modList, string version, EntityUserInfo account) : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private Task? _acceptLoopTask;

    private bool _disposed;

    private TcpListener? _listener;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _cts.Cancel();
            _listener?.Stop();
            try
            {
                _acceptLoopTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Log.Error("Authentication failed. {0}", ex.Message);
            }

            _cts.Dispose();
        }

        _disposed = true;
    }

    public void Start()
    {
        if (!_disposed)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _acceptLoopTask = AcceptLoopAsync(_cts.Token);
        }
        else
        {
            throw new ObjectDisposedException("AuthLibProtocol");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && !_disposed)
        {
            try
            {
                if (_listener != null)
                {
                    var client = await _listener.AcceptTcpClientAsync(token);
                    Log.Information("[AuthSock] Accepted: {0}", client.Client.RemoteEndPoint);
                    await HandleClientAsync(client, token);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex2)
            {
                Log.Warning("Accept loop error: {0}", ex2.Message);
                break;
            }
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken token)
    {
        int num;
        for (var read = 0; read < count; read += num)
        {
            num = await stream.ReadAsync(buffer.AsMemory(offset + read, count - read), token).ConfigureAwait(false);
            if (num == 0)
            {
                throw new EndOfStreamException();
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            await using var stream = client.GetStream();
            var responseCode = 1u;
            try
            {
                var lenBuf = new byte[4];

                await ReadExactAsync(stream, lenBuf, 0, 4, token);
                var gameIdLen = BitConverter.ToInt32(lenBuf);
                var gameIdBuf = new byte[gameIdLen];
                await ReadExactAsync(stream, gameIdBuf, 0, gameIdLen, token);
                var gameId = Encoding.UTF8.GetString(gameIdBuf);

                await ReadExactAsync(stream, lenBuf, 0, 4, token);
                var userIdLen = BitConverter.ToInt32(lenBuf);
                var userIdBuf = new byte[userIdLen];
                await ReadExactAsync(stream, userIdBuf, 0, userIdLen, token);
                // var userId = Encoding.UTF8.GetString(userIdBuf);

                await ReadExactAsync(stream, lenBuf, 0, 4, token);
                var serverIdLen = BitConverter.ToInt32(lenBuf);
                var serverIdBuf = new byte[serverIdLen];
                await ReadExactAsync(stream, serverIdBuf, 0, serverIdLen, token);
                var serverId = Encoding.UTF8.GetString(serverIdBuf);

                await NetEaseConnection.CreateAuthenticatorAsync(serverId, gameId, version, modList, account, success =>
                {
                    if (success)
                    {
                        responseCode = 0u;
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warning("Client handling error: {0}", ex.Message);
            }
            finally
            {
                try
                {
                    var bytes = BitConverter.GetBytes(responseCode);
                    await stream.WriteAsync(bytes, token);
                }
                catch (Exception ex2)
                {
                    Log.Warning("Response writing error: {0}", ex2.Message);
                }
            }
        }
    }
}