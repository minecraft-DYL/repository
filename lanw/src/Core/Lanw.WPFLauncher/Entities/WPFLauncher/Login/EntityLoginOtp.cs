using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.Login;

/// <summary>
/// X19 登录 OTP（由 Nirvana.WPFLauncher.Entities.WPFLauncher.Login.EntityLoginOtp 移植，自研）。
/// </summary>
public class EntityLoginOtp
{
    [JsonPropertyName("otp_token")]
    public string OtpToken { get; set; } = string.Empty;

    [JsonPropertyName("aid")]
    public int Aid { get; set; }

    [JsonPropertyName("lock_time")]
    public int LockTime { get; set; }

    [JsonPropertyName("open_otp")]
    public int OpenOtp { get; set; }
}