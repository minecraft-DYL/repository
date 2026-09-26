using System.Text;
using System.Text.Json;
using Lanw.Core;
using Lanw.Core.Entities;
using Lanw.Core.Entities.Login;
using Lanw.Core.Manager;
using Lanw.Core.Utils;
using Lanw.Core.Utils.CodeTools;
using Lanw.Public.Entities.Login;
using Lanw.Public.Entities.Nirvana;
using Lanw.Public.Message;

namespace Lanw.Core.Tests;

// 校验 Nirvana.Public 账号管理/登录编排 → Lanw.Public 移植后的行为（逻辑等价，自研命名）。
// 说明：持久化读写 PathUtil.ResourcePath/account.json（测试 bin 下 resources 目录），
// 每个用例先用 4399 型账号种子（无 UserId+Token 且非 cookie/163Email，故自动登录不触发网络），
// 再于 finally 恢复原文件并复位 InfoManager / 验证码静态。
public class AccountServiceTests
{
    private static string AccountPath => Path.Combine(PathUtil.ResourcePath, "account.json");

    private static IDisposable SeedAccounts(params EntityAccount[] seed)
    {
        Directory.CreateDirectory(PathUtil.ResourcePath);
        var backup = File.Exists(AccountPath) ? File.ReadAllText(AccountPath, Encoding.UTF8) : null;
        // 复位静态状态
        AccountMessage.Captcha4399 = null;
        AccountMessage.Captcha4399Bytes = null;
        InfoManager.GameAccountList.Clear();
        InfoManager.SetGameAccount(null);
        File.WriteAllText(AccountPath, JsonSerializer.Serialize(seed.ToList()), Encoding.UTF8);
        return new RestoreHandle(AccountPath, backup);
    }

    private static EntityAccount Mk4399(string account, string password = "p", string? name = null, string? userId = null)
    {
        return new EntityAccount { Type = "4399", Account = account, Password = password, Name = name, UserId = userId };
    }

    // --- 持久化 / Id 赋值 ---
    [Fact]
    public void AccountList_AssignsIds_InOrder_AndPreservesLoadedFields()
    {
        using var _ = SeedAccounts(Mk4399("a", name: "n0"), Mk4399("b", name: "n1"), Mk4399("c", name: "n2"));

        var list = AccountMessage.GetAccountList(safeUserId: false);
        Assert.Equal(3, list.Length);
        Assert.Equal(0, list[0].Id);
        Assert.Equal(1, list[1].Id);
        Assert.Equal(2, list[2].Id);
        Assert.Equal("a", list[0].Account);
        Assert.Equal("c", list[2].Account);
    }

    [Fact]
    public void SafeUserId_True_Masks_UnloggedAccounts_UserIdToken()
    {
        using var _ = SeedAccounts(Mk4399("x", name: "nx", userId: "U1"));

        // safeUserId=true：未登录账号的 UserId / Token 被清空（避免配置加载泄露）
        var safe = AccountMessage.GetAccountList();
        Assert.Null(safe[0].UserId);

        // safeUserId=false：保留配置加载的 UserId
        var raw = AccountMessage.GetAccountList(safeUserId: false);
        Assert.Equal("U1", raw[0].UserId);
    }

    // --- 切换 ---
    [Fact]
    public void SwitchAccount_NotFound_Throws()
    {
        using var _ = SeedAccounts(Mk4399("a"));

        var ex = Assert.Throws<ErrorCodeException>(() => AccountMessage.SwitchAccount(999));
        Assert.Equal(ErrorCode.NotFound, (ErrorCode)ex.Entity.Code!.Value);
    }

    [Fact]
    public void SwitchAccountToForce_SetsCurrentGameAccount()
    {
        using var _ = SeedAccounts(Mk4399("force-me", password: "pw"));

        AccountMessage.SwitchAccountToForce(0);
        var current = InfoManager.GetGameAccount();
        Assert.Equal("force-me", current.Account);
        Assert.Equal("pw", current.Password);
    }

    // --- 登录编排守卫（避免触发真实网络） ---
    [Fact]
    public void Login_NullPassword_Throws_PasswordError()
    {
        using var _ = SeedAccounts(new EntityAccount { Type = "4399", Account = "np", Password = null });

        var ex = Assert.Throws<ErrorCodeException>(() => AccountMessage.Login(0));
        Assert.Equal(ErrorCode.PasswordError, (ErrorCode)ex.Entity.Code!.Value);
    }

    [Fact]
    public void Login_4399_WithoutCaptcha_Throws_CaptchaNot()
    {
        using var _ = SeedAccounts(Mk4399("cap"));

        var ex = Assert.Throws<ErrorCodeException>(() => AccountMessage.Login(0));
        Assert.Equal(ErrorCode.CaptchaNot, (ErrorCode)ex.Entity.Code!.Value);
    }

    [Fact]
    public void GetCaptcha4399Content_NoBytes_Throws_Failure()
    {
        using var _ = SeedAccounts(); // 空种子，仅复位
        var ex = Assert.Throws<ErrorCodeException>(() => AccountMessage.GetCaptcha4399Content());
        Assert.Equal(ErrorCode.Failure, (ErrorCode)ex.Entity.Code!.Value);
    }

    // --- 增删改持久化（4399，不触发自动登录网络） ---
    [Fact]
    public void SaveAccount_Appends_NewAccount()
    {
        using var _ = SeedAccounts(); // 空文件

        AccountMessage.SaveAccount(Mk4399("newbie", name: "nn"));

        var list = AccountMessage.GetAccountList(safeUserId: false);
        Assert.Single(list);
        Assert.Equal("newbie", list[0].Account);
    }

    [Fact]
    public void DeleteAccount_RemovesById_AndReindexes()
    {
        using var _ = SeedAccounts(Mk4399("one"), Mk4399("two"));

        AccountMessage.DeleteAccount(0);

        var list = AccountMessage.GetAccountList(safeUserId: false);
        Assert.Single(list);
        Assert.Equal("two", list[0].Account);
        Assert.Equal(0, list[0].Id);
    }

    [Fact]
    public void UpdateAccount_ReplacesById()
    {
        using var _ = SeedAccounts(Mk4399("origA", name: "nA"), Mk4399("origB", name: "nB"));

        var replacement = Mk4399("replacedA", name: "rA");
        replacement.Id = 0;
        AccountMessage.UpdateAccount(replacement);

        var list = AccountMessage.GetAccountList(safeUserId: false);
        Assert.Equal(2, list.Length);
        Assert.Equal("replacedA", list[0].Account);
        Assert.Equal("rA", list[0].Name);
        Assert.Equal("origB", list[1].Account);
    }

    // --- 账号模块实体 ---
    [Fact]
    public void EntityGeeTest_Get_FormatsQuery()
    {
        var captcha = new EntityGeeTest
        {
            LotNumber = "L",
            PassToken = "P",
            GenTime = "G",
            CaptchaOutput = "C"
        };
        Assert.Equal("lot_number=L&pass_token=P&gen_time=G&captcha_output=C", captcha.Get());
    }

    [Fact]
    public void Entity4399CaptchaOk_Json_RoundTrip()
    {
        var entity = new Entity4399CaptchaOk { Captcha = "abc" };
        var json = JsonSerializer.Serialize(entity);
        var back = JsonSerializer.Deserialize<Entity4399CaptchaOk>(json);
        Assert.NotNull(back);
        Assert.Equal("abc", back!.Captcha);
    }

    [Fact]
    public void EntityNirvanaLogin_Derives_EntityResponseBase()
    {
        const string json = """{"code":1,"msg":"ok","online":"t0k3n"}""";
        var login = JsonSerializer.Deserialize<EntityNirvanaLogin>(json);
        Assert.NotNull(login);
        Assert.Equal(1, login!.Code);
        Assert.Equal("ok", login.Message);
        Assert.Equal("t0k3n", login.Token);
    }

    private sealed class RestoreHandle(string path, string? backup) : IDisposable
    {
        public void Dispose()
        {
            if (backup == null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                File.WriteAllText(path, backup, Encoding.UTF8);
            }

            InfoManager.GameAccountList.Clear();
            InfoManager.SetGameAccount(null);
            AccountMessage.Captcha4399 = null;
            AccountMessage.Captcha4399Bytes = null;
        }
    }
}