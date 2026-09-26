using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lanw.Core.Utils;
using Lanw.WPFLauncher.Entities;
using Lanw.WPFLauncher.Protocol;
using Lanw.WPFLauncher.Utils;

namespace Lanw.WPFLauncher.Http;

/// <summary>
/// 网易 X19 后台 HTTP 客户端（由 Nirvana.WPFLauncher.Http.X19Extensions 移植，自研）。
/// 内置多个远端端点（Gateway/Client/Core/Nirvana/Bmcl/Pt4399/UpdateNetease），
/// POST 时按需附加 user-id / user-token 签名头（TokenUtil）。
/// </summary>
public class X19Extensions(string url, bool token = true)
{
    /// <summary>解析选项：允许「本该是数字、服务端却发成字符串」的字段（如 days），避免形状差异直接爆解析异常。</summary>
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static readonly X19Extensions Gateway = new("https://x19apigatewayobt.nie.netease.com");
    public static readonly X19Extensions Client = new("https://x19mclobt.nie.netease.com");
    public static readonly X19Extensions Core = new("https://x19obtcore.nie.netease.com:8443", false);
    public static readonly X19Extensions Core1 = new("https://x19obtcore.nie.netease.com:8443");
    public static readonly X19Extensions Nirvana = new("http://110.42.70.32:13423", false);
    public static readonly X19Extensions Bmcl = new("https://bmclapi2.bangbang93.com", false);
    public static readonly X19Extensions Pt4399 = new("https://ptlogin.4399.com", false);
    public static readonly X19Extensions UpdateNetease = new("https://x19.update.netease.com", false);

    public readonly HttpWrapper HttpWrapper = new(url, options => { options.UserAgent("WPFLauncher/0.0.0.0"); });

    private async Task<HttpResponseMessage> ApiSend(string url, string? body = null, string? userId = null, string? userToken = null)
    {
        if (body == null)
        {
            return await HttpWrapper.GetAsync(url);
        }

        return await HttpWrapper.PostAsync(url, body, "application/json", options =>
        {
            if (userId != null && userToken != null)
            {
                options.AddHeaders(TokenUtil.Compute(url, body, userId, userToken));
            }
            else if (token)
            {
                options.AddHeaders(TokenUtil.Compute(url, body));
            }
        });
    }

    private async Task<HttpResponseMessage> ApiSendBytes(string url, byte[]? body = null)
    {
        return body == null ? await HttpWrapper.GetAsync(url) : await HttpWrapper.PostAsync(url, body);
    }

    private async Task<string> ApiAsync(string url, byte[]? body = null)
    {
        var response = await ApiSendBytes(url, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<T?> ApiBytes<T>(string url, byte[]? body = null)
    {
        var response = await ApiAsync(url, body);
        return ToType<T>(response, url);
    }

    public async Task<T?> ApiAsync<T>(string url, object? body = null, string? userId = null, string? userToken = null)
    {
        return await ApiAsync<T>(url, body == null ? null : JsonSerializer.Serialize(body, NPFLauncher.DefaultOptions), userId, userToken);
    }

    public T? Api<T>(string url, object? body = null, string? userId = null, string? userToken = null)
    {
        return ApiAsync<T>(url, body, userId, userToken).GetAwaiter().GetResult();
    }

    private async Task<T?> ApiAsync<T>(string url, string? body = null, string? userId = null, string? userToken = null)
    {
        var response = await ApiRawByString(url, body, userId, userToken);
        return ToType<T>(response, url);
    }

    internal static T? ToType<T>(string? response, string url)
    {
        if (response == null)
        {
            return default;
        }

        // 调用方显式要求原始文本/文档时不做强类型解析（原始文本可能就是非 JSON，如 patchlist）。
        if (typeof(T) == typeof(string))
        {
            return (T)(object)response;
        }

        try
        {
            if (typeof(T) == typeof(JsonDocument))
            {
                return (T)(object)JsonDocument.Parse(response);
            }

            return JsonSerializer.Deserialize<T>(response, ParseOptions);
        }
        catch (JsonException)
        {
            // 非 JSON / 形状不符：转成可读中文异常，绝不让 '$.data'/'\'0xE8\' is an invalid…' 这类
            // 原始解析错误冒泡到 UI。
            throw new EntityX19Exception(
                $"服务器响应无法解析（{url}）：{SafeJson.Preview(response)}",
                response);
        }
    }

    private async Task<string?> ApiRawByString(string url, string? body = null, string? userId = null, string? userToken = null)
    {
        var response = await ApiSend(url, body, userId, userToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<byte[]?> ApiAsyncRawB(string url, string? body = null, string? userId = null, string? userToken = null)
    {
        var response = await ApiSend(url, body, userId, userToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    public byte[]? ApiRawB(string url, string? body = null, string? userId = null, string? userToken = null)
    {
        return ApiAsyncRawB(url, body, userId, userToken).GetAwaiter().GetResult();
    }
}