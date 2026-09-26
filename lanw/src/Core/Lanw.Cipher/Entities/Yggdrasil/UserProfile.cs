using System.Text;
using System.Text.Json.Serialization;
using Lanw.Cipher.Cipher;
using Lanw.Cipher.Extensions;
using Lanw.Core.Entities.Login;

namespace Lanw.Cipher.Entities.Yggdrasil;

/// <summary>
/// 网易 Yggdrasil 用户侧档案（由 Nirvana.Cipher.Entities.Yggdrasil.UserProfile 移植，自研）。
/// 提供 authId（Skip32 加密 userId）与 authToken（ASCII 与 TokenKey 异或）。
/// </summary>
public class UserProfile
{
    private static readonly byte[] TokenKey =
    [
        0xAC, 0x24, 0x9C, 0x69, 0xC7, 0x2C, 0xB3, 0xB4,
        0x4E, 0xC0, 0xCC, 0x6C, 0x54, 0x3A, 0x81, 0x95
    ];

    [JsonPropertyName("user")]
    public required EntityUserInfo User { get; init; }

    public int GetUserId()
    {
        return int.Parse(User.GetUserId());
    }

    public int GetAuthId()
    {
        return Skip32Cipher.Encrypt(GetUserId(), "SaintSteve"u8.ToArray());
    }

    public byte[] GetAuthToken()
    {
        return Encoding.ASCII.GetBytes(User.GetToken()).Xor(TokenKey);
    }
}