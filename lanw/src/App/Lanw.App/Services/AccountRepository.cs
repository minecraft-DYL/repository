using System.Text;
using System.Text.Json;
using Lanw.Core.Entities.Login;
using Lanw.Core.Manager;
using Lanw.Core.Utils;

namespace Lanw.App.Services;

/// <summary>
/// 账号仓库：进程内直调 t6 账号持久化存储（account.json）。
/// 仅做列表读取 / 追加新增 / 删除 / 设为当前 等持久化与内存状态操作，
/// 不调用 AccountMessage.Login / AutoLogin 等登录协议（登录编排留待 t7c）。
/// 注意：Id 采用位置语义——由数组下标赋值（与 Lanw.Public.AccountMessage.GetAccountList1 一致），
/// 需用本仓库的 Load 重新读取后再增删，避免 Id 错位。
/// </summary>
public sealed class AccountRepository
{
    private const string AccountFileName = "account.json";

    private readonly object _lock = new();

    private string AccountPath => Path.Combine(PathUtil.ResourcePath, AccountFileName);

    /// <summary>
    /// 读取全部账号（受影响捕捉：account.json 同 t6，写回时保持原语义）。
    /// </summary>
    public IReadOnlyList<EntityAccount> Load()
    {
        return LoadCore();
    }

    /// <summary>
    /// 追加新增一个账号并写回。
    /// </summary>
    public void Append(EntityAccount account)
    {
        lock (_lock)
        {
            var current = LoadCore();
            Directory.CreateDirectory(PathUtil.ResourcePath);
            var next = current.Append(account).ToArray();
            WriteAll(next);
        }
    }

    /// <summary>
    /// 更新指定 Id 的账号并写回。
    /// </summary>
    public void Update(EntityAccount account)
    {
        lock (_lock)
        {
            var current = LoadCore().ToList();
            if (account.Id is < 0 or null)
            {
                throw new ArgumentException("账号 Id 无效，无法更新。");
            }

            if (account.Id.Value >= current.Count)
            {
                throw new ArgumentException("账号 Id 越界，无法更新。");
            }

            current[account.Id.Value] = account;
            WriteAll(current.ToArray());
        }
    }

    /// <summary>
    /// 删除指定 Id 的账号并写回。
    /// </summary>
    public void Delete(int id)
    {
        lock (_lock)
        {
            var current = LoadCore().ToList();
            if (id < 0 || id >= current.Count)
            {
                return; // 越界视为已删除
            }

            // 与 AccountMessage.DeleteAccount 一致：该槽位置空，读取时过滤 null
            current[id] = null!;
            WriteAll(current.ToArray());
        }
    }

    /// <summary>
    /// 将指定账号设为当前账号（进程内内存态，InfoManager）。
    /// </summary>
    public void SetCurrent(EntityAccount account)
    {
        InfoManager.SetGameAccount(account);
    }

    /// <summary>
    /// 当前账号：已登录/已设为当前的账号，无则 null。
    /// </summary>
    public EntityAccount? GetCurrent()
    {
        try
        {
            return InfoManager.GetGameAccount();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 判断给定账号（按 Equals 语义）是否为当前账号。
    /// </summary>
    public bool IsCurrent(EntityAccount account)
    {
        var current = GetCurrent();
        return current != null && current.Equals(account);
    }

    private List<EntityAccount> LoadCore()
    {
        // 复用 t6 的持久化读取，只取列表与路径（不触发 DefaultLogin 网络）。
        var (list, _) = Tools.GetValueOrDefaultList<EntityAccount>(AccountFileName);

        // Id = 数组下标（与 AccountMessage.GetAccountList1 位置语义一致）
        var index = -1;
        foreach (var item in list)
        {
            index++;
            item.Id = index;
        }

        return list.ToList();
    }

    private void WriteAll(EntityAccount[] accounts)
    {
        Directory.CreateDirectory(PathUtil.ResourcePath);
        File.WriteAllText(AccountPath, JsonSerializer.Serialize(accounts), Encoding.UTF8);
    }
}