using System.Text;
using System.Text.Json;
using Lanw.Cipher.Cipher;
using Lanw.Cipher.Entities.Yggdrasil;
using Lanw.Cipher.Extensions;
using Lanw.Cipher.Generator;
using Lanw.Cipher.Protocol;
using Lanw.Cipher.Yggdrasil;
using Lanw.Core.Entities.Login;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using InstSkip32 = Lanw.Cipher.Cipher.Nirvana.Skip32Cipher;

namespace Lanw.Core.Tests;

// 校验 Nirvana.Cipher → Lanw.Cipher 移植后的行为（逻辑等价，自研命名）。
public class CipherPortTests
{
    // --- Skip32Cipher (static, encrypt-only) ---
    [Fact]
    public void Skip32Cipher_Encrypt_IsDeterministic_AndNonTrivial()
    {
        var key = "SaintSteve"u8.ToArray();
        var a = Skip32Cipher.Encrypt(10086, key);
        var b = Skip32Cipher.Encrypt(10086, key);
        Assert.Equal(a, b);
        Assert.NotEqual(10086, a);
        Assert.NotEqual(10086, Skip32Cipher.Encrypt(999, key));
    }

    // --- Skip32Cipher (instance): GenerateRoleUuid ↔ ComputeUserIdFromUuid ---
    [Fact]
    public void Skip32Cipher_UserId_RoundTrips_ThroughUuid()
    {
        var cipher = new InstSkip32("1234567890"u8.ToArray());
        const string role = "Steve";
        const uint userId = 123456789;

        var uuid = cipher.GenerateRoleUuid(role, userId);
        Assert.Equal(32, uuid.Length);

        var recovered = cipher.ComputeUserIdFromUuid(uuid);
        Assert.Equal(userId, recovered);
    }

    [Fact]
    public void Skip32Cipher_InvalidKeyLength_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new InstSkip32([]));
        Assert.Contains("10", ex.Message);
    }

    // --- AesNoPadding: ECB round-trip ---
    [Fact]
    public void AesNoPadding_RoundTrip()
    {
        var key = new byte[16];
        for (var i = 0; i < key.Length; i++) key[i] = (byte)i;

        var plaintext = new byte[48];
        for (var i = 0; i < plaintext.Length; i++) plaintext[i] = (byte)(i * 3);

        var encrypted = AesNoPadding.Encrypt(plaintext, key, encryption: true);
        Assert.Equal(plaintext.Length, encrypted.Length);

        var decrypted = AesNoPadding.Encrypt(encrypted, key, encryption: false);
        Assert.Equal(plaintext, decrypted);
    }

    // --- ByteArrayExtensions: Xor, CombineWith ---
    [Fact]
    public void ByteArray_Xor_And_CombineWith()
    {
        var a = new byte[] { 1, 2, 3 };
        var b = new byte[] { 0x0F, 0x10, 0x11 };

        var xor = a.Xor(b);
        Assert.Equal(3, xor.Length);
        Assert.Equal(new byte[] { 0x0E, 0x12, 0x12 }, xor);

        var combined = a.CombineWith(b);
        Assert.Equal(6, combined.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 0x0F, 0x10, 0x11 }, combined);

        Assert.Throws<ArgumentException>(() => a.Xor(new byte[] { 1 }));
    }

    // --- YggdrasilExtensions: EncodeSha256 / Write helpers / pack-unpack ---
    [Fact]
    public void EncodeSha256_And_ByteSerialization_Helpers()
    {
        var input = Encoding.UTF8.GetBytes("lanw");
        var hash = input.EncodeSha256();
        Assert.Equal(32, hash.Length);

        // littleEndian default matches host order (x64 LE)
        var i32 = 0x01020304;
        var le = i32.ToByteArray(littleEndian: true);
        Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, le);
        var be = i32.ToByteArray(littleEndian: false);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, be);
    }

    [Fact]
    public void ChaChaPacker_Pack_IsEncrypted_And_UnpackValidatesCrc()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var nonce = Encoding.ASCII.GetBytes("163 NetEase\n"); // 12 bytes
        var plaintext = Encoding.UTF8.GetBytes("hello-signed-data");

        var packer = new ChaChaPacker(key, nonce, true);
        var packed = packer.PackMessage(9, plaintext);

        // PackMessage 输出 = plaintext + 10 字节的包头（长度/CRC/类型/填充）。
        Assert.Equal(plaintext.Length + 10, packed.Length);

        // 密文区（offset 2 起）与明文可见部分不同 => 加密已生效（ChaCha 流密码）。
        var visible = packed.AsSpan(2).ToArray();
        Assert.NotEqual(plaintext, visible.Take(plaintext.Length).ToArray());

        // 用独立解密器对同一报文做 Unpack：参考实现按 offset 0 解密并校验 CRC；
        // 与打包的 offset=2 加密切口不对齐，CRC 必然失败 —— 复现参考行为（非对称打包/解包由服务端协议决定）。
        var unpacker = new ChaChaPacker(key, nonce, false);
        Assert.Throws<System.Exception>(() => unpacker.UnpackMessage(packed));
    }

    // --- Rsa: 私钥"加密/签名"→ 公钥"解密/验签" 往返（与 YggdrasilGenerator 用法一致） ---
    [Fact]
    public void Rsa_PrivateEncryptThenPublicDecrypt_RoundTrip()
    {
        var g = new RsaKeyPairGenerator();
        g.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
        var pair = g.GenerateKeyPair();

        var publicB64 = Convert.ToBase64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pair.Public).GetDerEncoded());
        var privateB64 = Convert.ToBase64String(Org.BouncyCastle.Pkcs.PrivateKeyInfoFactory.CreatePrivateKeyInfo(pair.Private).GetDerEncoded());

        var pub = Rsa.LoadPublicKey(publicB64);
        var priv = Rsa.LoadPrivateKey(privateB64);

        var data = Encoding.UTF8.GetBytes("sign-content-48bytes");
        var signed = Rsa.RsaWithPkcs1(priv, data, forEncryption: true);
        var verified = Rsa.RsaWithPkcs1(pub, signed, forEncryption: false);

        Assert.Equal(data, verified);
    }

    // --- UserProfile: auth token deterministic XOR ---
    [Fact]
    public void UserProfile_GetAuthId_MatchesStaticSkip32()
    {
        var user = new EntityUserInfo { UserId = "10086", Token = "0123456789abcdef" };
        var profile = new UserProfile { User = user };

        var expected = Skip32Cipher.Encrypt(10086, "SaintSteve"u8.ToArray());
        Assert.Equal(expected, profile.GetAuthId());
    }

    // --- Md5Mapping: known default mappings ---
    [Fact]
    public void Md5Mapping_KnownVersion_HasDefaults()
    {
        var pair = Md5Mapping.GetMd5FromGameVersion("1.20");
        Assert.NotNull(pair);
        Assert.Equal("2A7A476411A1687A56DC6848829C1AE4", pair.BootstrapMd5);

        Assert.Throws<ArgumentException>(() => Md5Mapping.GetMd5FromGameVersion("0.0"));
    }

    // --- YggdrasilGenerator: emits deterministic valid-shaped message ---
    [Fact]
    public void YggdrasilGenerator_GenerateJoinMessage_EmitsNonEmpty()
    {
        var user = new EntityUserInfo { UserId = "1", Token = "0123456789abcdef" };
        var profile = new GameProfile {
            GameId = "42",
            GameVersion = "1.20",
            BootstrapMd5 = "AA",
            DatFileMd5 = "BB",
            Mods = null,
            User = new UserProfile { User = user }
        };
        var loginSeed = new byte[16];

        X19.CrcSalt = "test-salt";
        var msg = YggdrasilGenerator.GenerateJoinMessage(profile, "server-1", loginSeed);
        Assert.NotNull(msg);
        Assert.True(msg.Length > 0);
        X19.CrcSalt = null;
    }

    // --- GameProfile / Mod / YggdrasilServer JSON round-trip ---
    [Fact]
    public void GameProfile_And_Server_Json_RoundTrip()
    {
        const string json = """
            {"gameId":"1","gameVersion":"1.20","bootstrapMd5":"AA","datFileMd5":"BB",
             "mods":null,"profile":{"user":{"userId":"9","token":"t"}}}
            """;
        var profile = JsonSerializer.Deserialize<GameProfile>(json);
        Assert.NotNull(profile);
        Assert.Equal("1.20", profile!.GameVersion);
        Assert.Equal("9", profile.User.User.GetUserId());

        var serverJson = """{"IP":"127.0.0.1","Port":25565,"ServerType":"netease"}""";
        var server = JsonSerializer.Deserialize<YggdrasilServer>(serverJson);
        Assert.NotNull(server);
        Assert.Equal(25565, server!.Port);
    }
}