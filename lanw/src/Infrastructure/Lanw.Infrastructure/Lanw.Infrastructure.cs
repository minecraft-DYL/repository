namespace Lanw.Infrastructure;

/// <summary>
/// Infrastructure 基础设施层根标记。
///
/// 分层（参照 Fantnel 思路，代码自研）：
///   - Cipher      本地敏感数据加解密（DPAPI + 自研签名，替代 Fantnel 明文 JSON）
///   - Storage     SQLite / 本地文件 存储
///   - Networking  网络/下载 封装（无 HTTP 内嵌演进，必要时 Mock）
///   - Providers  数据源适配（可一键替换，后续接真实协议）
///
/// 首个待移植功能：账号安全存储 + 登录 Provider 适配，见后续阶段任务。
/// </summary>
public static class LanwInfrastructure
{
    /// <summary>基础设施层程序集名（用于定位/单测）。</summary>
    public const string AssemblyName = "Lanw.Infrastructure";
}