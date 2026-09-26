using System.Text.Json.Serialization;

namespace Lanw.Core.Entities.Login;

// 游戏账号（由 Nirvana.Common.Entities.Login.EntityAccount 移植，自研）
public class EntityAccount : EntityUserInfo {
    // 基础信息
    [JsonPropertyName("name")]
    [JsonInclude]
    public string? Name { get; set; }

    [JsonPropertyName("account")]
    [JsonInclude]
    public string? Account { get; set; }

    [JsonPropertyName("type")]
    [JsonInclude]
    public string? Type { get; set; }

    [JsonPropertyName("password")]
    [JsonInclude]
    public string? Password { get; init; }

    // 识别信息
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>
    /// 根据 基础信息 判断 是否 是 同一个账号。
    /// </summary>
    public bool Equals(EntityAccount other)
    {
        // cookie 用 值 判断
        if (Type == "cookie" && other.Type == "cookie") {
            return Password == other.Password;
        }

        // 账号 密码 登录类型 一致 则 认为 是 同一个账号
        return Account == other.Account && Type == other.Type && Password == other.Password;
    }

    public new string ToString()
    {
        return Type == "cookie" ? $"Type: {Type}, Password: {Password}" : $"Account: {Account}, Type: {Type}, Password: {Password}";
    }

    public bool IsConfig()
    {
        // 主动登录游戏
        if (!LanwConfig.GetValue<bool>("autoLoginGame")) {
            return false;
        }

        // 主动登录 163Email
        if ("163Email".Equals(Type, StringComparison.OrdinalIgnoreCase)) {
            return LanwConfig.GetValue<bool>("autoLoginGame163Email");
        }

        // 主动登录 Cookie
        return "cookie".Equals(Type) && LanwConfig.GetValue<bool>("autoLoginGameCookie");
    }
}