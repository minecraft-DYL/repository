using System;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lanw.Cipher.Protocol;

/// <summary>
/// 网易 X19 后台 HTTP API 访问器（由 Nirvana.WPFLauncher.Http.X19Extensions 移植的最小面，自研）。
///
/// 说明：完整 X19Extensions 属 WPFLauncher HTTP 层（内置 HttpWrapper / TokenUtil 签名）。
/// Cipher 仅用到 `Nirvana` 与 `UpdateNetease` 两个 token=false 端点，故此处只提供 Api/ApiAsync
/// 的最小实现（HttpClient 直连 + UnsafeRelaxedJsonEscaping），待 WPFLauncher 移植时统一收敛。
/// </summary>
public class NeteaseApi(string url)
{
    public static readonly NeteaseApi Nirvana = new("http://110.42.70.32:13423");
    public static readonly NeteaseApi UpdateNetease = new("https://x19.update.netease.com");

    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public T? Api<T>(string path, object? body = null)
    {
        return ApiAsync<T>(path, body).GetAwaiter().GetResult();
    }

    public async Task<T?> ApiAsync<T>(string path, object? body = null)
    {
        var response = body == null
            ? await _client.GetStringAsync(BuildUrl(path)).ConfigureAwait(false)
            : await PostAsJsonAsync(path, body).ConfigureAwait(false);

        return ToType<T>(response);
    }

    private string BuildUrl(string path)
    {
        return url.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private async Task<string> PostAsJsonAsync(string path, object body)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(BuildUrl(path), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static T? ToType<T>(string? response)
    {
        if (response == null)
        {
            return default;
        }

        if (typeof(T) == typeof(JsonDocument))
        {
            return (T)(object)JsonDocument.Parse(response);
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)response;
        }

        return JsonSerializer.Deserialize<T>(response);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}