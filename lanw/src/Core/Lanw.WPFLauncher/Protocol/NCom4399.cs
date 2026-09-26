using System.Net;
using System.Net.Http;
using System.Text.Json;
using Lanw.Core.Utils;
using Lanw.WPFLauncher.Entities.Pc4399.Com4399;
using Lanw.WPFLauncher.Utils;

namespace Lanw.WPFLauncher.Protocol;

/// <summary>
/// 4399.com 账号密码登录（由 Nirvana.WPFLauncher.Protocol.NCom4399 移植，自研）。
/// </summary>
public static class NCom4399
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new CookieContainer()
    });

    public static string LoginWithPassword(string username, string password, string sessionId, string captcha)
    {
        return LoginWithPasswordAsync(username, password, sessionId, captcha).GetAwaiter().GetResult();
    }

    private static async Task<string> LoginWithPasswordAsync(string username, string password, string sessionId, string captcha)
    {
        var oauthResp = await Client.GetAsync("https://m.4399api.com/openapi/oauth-callback.html?gamekey=44770&game_key=115716");
        var oauthText = await oauthResp.Content.ReadAsStringAsync();
        var oauthCallback = SafeJson.Deserialize<Entity4399OAuthCallback>(oauthText, "4399 OAuth 回调(/openapi/oauth-callback.html)");
        if (oauthCallback == null)
        {
            throw new Exception("4399 授权回调解析失败：" + SafeJson.Preview(oauthText));
        }

        var queryParams = QueryBuilder.FromParameters(oauthCallback.Result);
        var clientId = queryParams.Get("client_id");
        var state = queryParams.Get("state");
        var ref1 = queryParams.Get("ref");

        // 构建登录参数
        var parameters = BuildLoginParameters(clientId, state, ref1);
        parameters.Add("username", username);
        parameters.Add("password", password);
        parameters.Add("captcha_id", captcha);
        parameters.Add("captcha", sessionId);

        // 执行登录请求
        var loginResponse = await Client.PostAsync("https://ptlogin.4399.com/oauth2/loginAndAuthorize.do?channel=&sdk=op&sdk_version=3.14.5.577", new FormUrlEncodedContent(parameters.GetAll()));

        var loginText = await loginResponse.Content.ReadAsStringAsync();

        // 找到错误信息
        var errText = ExtractErrorTip(loginText);
        if (errText.Length > 0)
        {
            throw new Exception(errText);
        }

        var userInfoResponse = SafeJson.Deserialize<Entity4399UserInfoResponse>(loginText, "4399com 登录响应(ptlogin/loginAndAuthorize.do)");

        if (userInfoResponse == null)
        {
            throw new Exception("4399com 登录响应解析失败：" + SafeJson.Preview(loginText));
        }

        if (userInfoResponse.Code != "100")
        {
            throw new Exception(string.IsNullOrWhiteSpace(userInfoResponse.Message)
                ? "4399com 登录失败：" + SafeJson.Preview(loginText)
                : userInfoResponse.Message);
        }

        var entity4399UserInfoResult = userInfoResponse.Result;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (entity4399UserInfoResult == null)
        {
            throw new Exception("Failed to deserialize: " + loginText);
        }

        // 生成SAuth令牌
        return MgbSdk.GenerateSAuth(entity4399UserInfoResult.Uid.ToString(), entity4399UserInfoResult.State, "4399com", "ad");
    }

    private static string ExtractErrorTip(string html)
    {
        var text = html.Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        // 成功负载是 JSON（形如 {"code":"100",...}）→ 不是错误提示。
        if (text[0] is '{' or '[')
        {
            return string.Empty;
        }

        const string startMarker = "login_err_msg\">";
        const string endMarker = "</p>";

        var startIndex = html.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIndex == -1)
        {
            // 非 HTML 且非 JSON：整段就是纯文本错误（4399 有时直接返回中文错误页）；
            // 若是 HTML 但没找到错误标记，则交给后续 JSON 解析统一报错。
            return html.Contains("<html>", StringComparison.OrdinalIgnoreCase) ? string.Empty : text;
        }

        startIndex += startMarker.Length;
        var endIndex = html.IndexOf(endMarker, startIndex, StringComparison.Ordinal);

        if (endIndex == -1)
        {
            return string.Empty;
        }

        // 提取内容并删除前后空格
        var content = html.Substring(startIndex, endIndex - startIndex);
        return content.Trim();
    }

    private static QueryBuilder BuildLoginParameters(string clientId, string state, string ref1)
    {
        var queryBuilder = new QueryBuilder();
        queryBuilder.Add("isInputRealname", "false");
        queryBuilder.Add("isVaildRealname", "false");
        queryBuilder.Add("sec", "0");
        queryBuilder.Add("client_id", clientId);
        queryBuilder.Add("state", state);
        queryBuilder.Add("ref", ref1);
        queryBuilder.Add("response_type", "TOKEN");
        queryBuilder.Add("scope", "basic");
        queryBuilder.Add("bizId", "2100001792");
        queryBuilder.Add("auth_action", "ORILOGIN");
        queryBuilder.Add("redirect_uri", "https://m.4399api.com/openapi/oauth-callback.html?gamekey=44770&game_key=115716");
        return queryBuilder;
    }
}