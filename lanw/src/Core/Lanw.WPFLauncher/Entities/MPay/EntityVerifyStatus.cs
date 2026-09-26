using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 校验状态（由 Nirvana.WPFLauncher.Entities.MPay.EntityVerifyStatus 移植，自研）。
/// </summary>
public class EntityVerifyStatus
{
    [JsonPropertyName("need_sms")]
    public int NeedSms { get; set; }

    [JsonPropertyName("need_email")]
    public int NeedEmail { get; set; }

    [JsonPropertyName("need_passwd")]
    public int NeedPasswd { get; set; }

    [JsonPropertyName("need_real_name")]
    public int NeedRealName { get; set; }
}