using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lanw.Core.Utils.CodeTools;
using Lanw.WPFLauncher.Entities;
using Lanw.WPFLauncher.Entities.WPFLauncher.Login;
using Lanw.WPFLauncher.Http;
using Lanw.WPFLauncher.Protocol;
using Lanw.WPFLauncher.Utils;
using Lanw.WPFLauncher.Utils.Cipher;
using RequestTokenUtil = Lanw.WPFLauncher.Utils.TokenUtil;
using CipherTokenUtil = Lanw.WPFLauncher.Utils.Cipher.TokenUtil;

namespace Lanw.Core.Tests;

// 校验 Nirvana.WPFLauncher 登录认证层 → Lanw.WPFLauncher 移植后的行为（逻辑等价，自研命名）。
public class WPFLauncherAuthTests
{
    // --- MPayExtensions: 编码/散列 ---
    [Fact]
    public void MPayExtensions_Hex_Md5_Base64()
    {
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", "abc".EncodeMd5()); // MD5("abc")
        Assert.Equal("616263", Encoding.UTF8.GetBytes("abc").EncodeHex()); // "abc" hex
        Assert.Equal("YWJj", "abc".EncodeBase64());
        Assert.Equal(Encoding.UTF8.GetBytes("abc"), "616263".DecodeHex());
        Assert.Equal(string.Empty, "".EncodeBase64());
    }

    [Fact]
    public void MPayExtensions_EncodeAes_RoundTrips_EcbPkcs7()
    {
        var key = new byte[16];
        for (var i = 0; i < key.Length; i++) key[i] = (byte)(i + 1);

        var cipher = "secret-plaintext".EncodeAes(key);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        var plain = aes.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length);

        Assert.Equal("secret-plaintext", Encoding.UTF8.GetString(plain));
    }

    // --- QueryBuilder ---
    [Fact]
    public void QueryBuilder_BuildAndParse()
    {
        var qb = new QueryBuilder();
        qb.Add("a", "1").Add("b", "hello world").Add("c", "");
        var query = qb.BuildQuery();
        Assert.DoesNotContain("c=", query);          // 空值被过滤
        Assert.Contains("a=1", query);
        Assert.Contains("b=hello%20world", query);

        var parsed = new QueryBuilder("x=1&y=two words&z=");
        Assert.Equal("1", parsed.Get("x"));
        Assert.Equal("two words", parsed.Get("y"));

        var fromUrl = QueryBuilder.FromParameters("https://h?p=1&q=2");
        Assert.Equal("1", fromUrl.Get("p"));
        Assert.Equal("2", fromUrl.Get("q"));

        Assert.Throws<Exception>(() => new QueryBuilder("k=v").Get("missing"));
    }

    // --- StringGenerator ---
    [Fact]
    public void StringGenerator_ProducesExpectedFormats()
    {
        Assert.Equal(32, StringGenerator.GenerateHexString(16).Length); // byte[16] → 32 hex 字符
        var mac = StringGenerator.GenerateRandomMacAddress();
        Assert.Equal(17, mac.Length);
        Assert.Equal(':', mac[2]);
        var rand = StringGenerator.GenerateRandomString(12);
        Assert.Equal(12, rand.Length);
        Assert.Throws<ArgumentException>(() => StringGenerator.GenerateRandomString(0));
    }

    // --- TokenUtil (X19 请求签名头) ---
    [Fact]
    public void TokenUtil_Compute_Deterministic()
    {
        var t1 = RequestTokenUtil.Compute("/login-otp", "{}", "u1", "tok");
        var t2 = RequestTokenUtil.Compute("/login-otp", "{}", "u1", "tok");
        Assert.True(t1.ContainsKey("user-id"));
        Assert.True(t1.ContainsKey("user-token"));
        Assert.Equal("u1", t1["user-id"]);
        Assert.Equal(t1["user-token"], t2["user-token"]);

        // 不同 body => 不同 token
        var t3 = RequestTokenUtil.Compute("/login-otp", "{\"a\":1}", "u1", "tok");
        Assert.NotEqual(t1["user-token"], t3["user-token"]);
    }

    // --- Utils.Cipher.TokenUtil：加密令牌 ---
    [Fact]
    public void CipherTokenUtil_GenerateEncryptToken_IsHex_AndNonEmpty()
    {
        var token = CipherTokenUtil.GenerateEncryptToken("abc123");
        Assert.False(string.IsNullOrEmpty(token));
        Assert.All(token, ch => Assert.True(Uri.IsHexDigit(ch)));
    }

    // --- HttpUtil：authentication-otp 报文加解密往返 ---
    [Fact]
    public void HttpUtil_EncryptRoundTrips_ThroughDecrypt()
    {
        var body = Encoding.UTF8.GetBytes("{\"sa_data\":\"x\",\"otp_token\":\"y\"}");
        var encrypted = HttpUtil.HttpEncrypt(body);
        Assert.True(encrypted.Length > 16);

        var decrypted = HttpUtil.HttpDecrypt(encrypted);
        Assert.NotNull(decrypted);
        Assert.Equal(body, decrypted!);
    }

    // --- MgbSdk.GenerateSAuth：JSON 结构 ---
    [Fact]
    public void MgbSdk_GenerateSAuth_Serializes_WithGameIdX19()
    {
        var json = MgbSdk.GenerateSAuth("100", "SESSION", "4399pc", "pc", "nick", "12345");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("100", root.GetProperty("sdkuid").GetString());
        Assert.Equal("SESSION", root.GetProperty("sessionid").GetString());
        Assert.Equal("x19", root.GetProperty("gameid").GetString());
        Assert.Equal("12345", root.GetProperty("timestamp").GetString());
    }

    // --- EntityX19Exception ---
    [Fact]
    public void EntityX19Exception_Carries_Entity()
    {
        var ex = new EntityX19Exception("boom", new { code = 1 });
        Assert.Equal("boom", ex.Message);
        Assert.NotNull(ex.Data);
    }

    // --- 登录实体 JSON 往返 ---
    [Fact]
    public void LoginEntities_Json_RoundTrip()
    {
        var cookie = new EntityX19Cookie
        {
            SdkUid = "1",
            SessionId = "s",
            Udid = "u",
            DeviceId = "d"
        };
        var cookieJosn = JsonSerializer.Serialize(cookie);
        var back = JsonSerializer.Deserialize<EntityX19Cookie>(cookieJosn);
        Assert.NotNull(back);
        Assert.Equal("netease", back!.LoginChannel);
        Assert.Equal("1", back.SdkUid);

        var otp = new EntityAuthenticationOtp { Token = "tok", Account = "acct" };
        var otpJson = JsonSerializer.Serialize(otp);
        Assert.Contains("\"token\"", otpJson);
        var otpBack = JsonSerializer.Deserialize<EntityAuthenticationOtp>(otpJson);
        Assert.Equal("tok", otpBack!.Token);
    }

    // --- 错误码体系 Linchpin：X19 未设置 CrcSalt 时抛 ErrorCodeException ---
    [Fact]
    public void X19_GetCrcSalt_Throws_WhenUnset()
    {
        var original = X19.CrcSalt;
        try
        {
            X19.CrcSalt = null;
            var ex = Assert.Throws<ErrorCodeException>(() => X19.GetCrcSalt());
            Assert.Equal(ErrorCode.CrcSaltNotSet, (ErrorCode)ex.Entity.Code!.Value);
        }
        finally
        {
            X19.CrcSalt = original;
        }
    }
}