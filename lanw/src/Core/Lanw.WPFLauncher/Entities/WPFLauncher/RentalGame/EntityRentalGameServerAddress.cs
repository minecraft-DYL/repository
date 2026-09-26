using System.Text.Json.Serialization;

namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;

/// <summary>
/// 租赁服连接地址（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.EntityRentalGameServerAddress 移植，自研）。
/// 含移动/电信/联通三线接入地址与启用开关。
/// </summary>
public class EntityRentalGameServerAddress
{
    [JsonPropertyName("mcserver_host")]
    public string McServerHost { get; set; } = string.Empty;

    [JsonPropertyName("mcserver_port")]
    public ushort McServerPort { get; set; }

    [JsonPropertyName("state")]
    public EnumServerStatus State { get; set; }

    [JsonPropertyName("cmcc_mcserver_host")]
    public string CmccMcServerHost { get; set; } = string.Empty;

    [JsonPropertyName("cmcc_mcserver_port")]
    public int CmccMcServerPort { get; set; }

    [JsonPropertyName("ctcc_mcserver_host")]
    public string CtccMcServerHost { get; set; } = string.Empty;

    [JsonPropertyName("ctcc_mcserver_port")]
    public int CtccMcServerPort { get; set; }

    [JsonPropertyName("cucc_mcserver_host")]
    public string CuccMcServerHost { get; set; } = string.Empty;

    [JsonPropertyName("cucc_mcserver_port")]
    public int CuccMcServerPort { get; set; }

    [JsonPropertyName("isp_enable")]
    public bool IspEnable { get; set; }
}
