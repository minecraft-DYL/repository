using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.MPay;

/// <summary>
/// MPay 短信登录票据（由 Nirvana.WPFLauncher.Entities.MPay.EntitySmsTicket 移植，自研）。
/// </summary>
public class EntitySmsTicket
{
    [JsonPropertyName("guide_text")]
    public string GuideText { get; set; } = string.Empty;

    [JsonPropertyName("related_emails")]
    public string[] RelatedEmails { get; set; } = Array.Empty<string>();

    [JsonPropertyName("ticket")]
    public string Ticket { get; set; } = string.Empty;

    [JsonPropertyName("related_accounts")]
    public string[] RelatedAccounts { get; set; } = Array.Empty<string>();
}