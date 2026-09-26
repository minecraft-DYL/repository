using System.Net.Http;
using System.Text.Json;
using Lanw.Core.Utils;
using Lanw.WPFLauncher.Entities.Pc4399;
using Lanw.WPFLauncher.Http;

namespace Lanw.WPFLauncher.Protocol;

/// <summary>
/// MGB SDK（生成 SAuth 令牌 / 认证会话）（由 Nirvana.WPFLauncher.Protocol.MgbSdk 移植，自研）。
/// </summary>
public class MgbSdk(string gameId) : IDisposable
{
    private readonly HttpWrapper _sdk = new("https://mgbsdk.matrix.netease.com");

    public void Dispose()
    {
        _sdk.Dispose();
        GC.SuppressFinalize(this);
    }

    public static string GenerateSAuth(string sdkUid, string sessionId, string channel, string platform, string userId = "", string timestamp = "")
    {
        var str = Guid.NewGuid().ToString("N");
        return JsonSerializer.Serialize(new EntityMgbSdkSAuthJson
        {
            AppChannel = channel,
            ClientLoginSn = str,
            DeviceId = str,
            GameId = "x19",
            LoginChannel = channel,
            SdkUid = sdkUid,
            SessionId = sessionId,
            Timestamp = timestamp,
            Platform = platform,
            SourcePlatform = platform,
            Udid = str,
            UserId = userId
        });
    }

    public async Task AuthSession(string cookie)
    {
        var httpResponseMessage = await _sdk.PostAsync($"/{gameId}/sdk/uni_sauth", cookie);
        var responseText = await httpResponseMessage.Content.ReadAsStringAsync();
        var dictionary = SafeJson.Deserialize<Dictionary<string, object>>(responseText, "MGB SDK 认证(/sdk/uni_sauth)");
        if (dictionary == null)
        {
            throw new HttpRequestException("MGB SDK 认证响应为空：" + SafeJson.Preview(responseText));
        }

        if (dictionary.TryGetValue("code", out var code) && "200".Equals(code?.ToString()))
        {
            return;
        }

        var message = dictionary.TryGetValue("msg", out var msg) && !string.IsNullOrWhiteSpace(msg?.ToString())
            ? msg!.ToString()
            : dictionary.TryGetValue("status", out var status) && !string.IsNullOrWhiteSpace(status?.ToString())
                ? status!.ToString()
                : SafeJson.Preview(responseText);

        throw new HttpRequestException(message);
    }
}