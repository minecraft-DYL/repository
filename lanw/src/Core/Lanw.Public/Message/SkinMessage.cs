using System.Threading;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;
using Lanw.WPFLauncher.Protocol;

namespace Lanw.Public.Message;

/// <summary>
/// 皮肤列表/搜索编排（由 Nirvana.Public.Message.SkinMessage 移植，自研）。
/// 维护进程内皮肤列表缓存（分页拉取 + 按 EntityId 去重合并），并对缺失图片的条目从详情页修复图片。
/// 注：原参考在分页/合并流程中调用 CacheManager.DownloadCacheImage()/GetCacheImageUrl(item)
/// 将皮肤图片缓存到本地并把 TitleImageUrl 改写为 /image/skin/<id>.png，
/// 缓存引擎属网络服/皮肤缓存模块（t14 移植中），此处省略该副作用调用，
/// TitleImageUrl 保持远端 URL；缓存模块落地后可恢复该行为。
/// </summary>
public static class SkinMessage
{
    // 皮肤列表 - 缓存
    public static readonly List<EntityQueryNetSkinItem> SkinList = [];

    /// <summary>
    /// 获取皮肤列表（分页，优先进程内缓存，不足时按缓存长度继续拉取）。
    /// @param offset 偏移量
    /// @param pageSize 数量
    /// @param safeImage 是否修复缺失图片的条目（从详情页补图）
    /// @return 皮肤列表
    /// </summary>
    public static async Task<EntityQueryNetSkinItem[]> GetSkinList(int offset = 0, int pageSize = 10, bool safeImage = true)
    {
        var index = -pageSize; // 循环次数
        var count = offset + pageSize;

        while (true)
        {
            // 缓存图片下载（CacheManager.DownloadCacheImage，t14 移植中，暂省略）

            // SkinList 有 就用缓存
            // 分页
            if (SkinList.Count >= count)
            {
                var list = SkinList.Skip(offset).Take(pageSize).ToArray();
                // 无须修复图片
                if (!safeImage)
                {
                    return list;
                }

                // 修复没有图片的游戏项
                foreach (var item in list)
                {
                    // 没有图片
                    if (item.TitleImageSafe())
                    {
                        continue;
                    }

                    // 从 详情页 获取图片
                    await GetFirstImageByCache(item);
                }

                return list;
            }

            if (++index > 0)
            {
                // 最后一页, 减少数量，避免丢失数据
                count--;
                pageSize--;
                if (pageSize <= 0)
                {
                    return [];
                }
            }
            else
            {
                var items = await NPFLauncher.GetFreeSkinListAsync(SkinList.Count);
                AddSkinList(items);
                Thread.Sleep(500);
            }
        }
    }

    private static void AddSkinList(EntityQueryNetSkinItem[] skinItems)
    {
        foreach (var item in skinItems)
        {
            AddSkinList(item);
        }
    }

    // 皮肤列表 - 添加（按 EntityId 去重合并）
    private static void AddSkinList(EntityQueryNetSkinItem skinItem)
    {
        foreach (var item in SkinList.Where(item => item.EntityId == skinItem.EntityId))
        {
            if (item.TitleImageUrl == "" && skinItem.TitleImageUrl != "")
            {
                item.TitleImageUrl = skinItem.TitleImageUrl;
                // CacheManager.GetCacheImageUrl(item)：皮肤图片缓存（t14 移植中，暂省略）
            }

            return;
        }

        SkinList.Add(skinItem);
    }

    /// <summary>
    /// 获取皮肤的第一张图片（来自详情页）。
    /// @param item 皮肤条目
    /// </summary>
    private static async Task GetFirstImage(EntityQueryNetSkinItem item)
    {
        var details = await NPFLauncher.GetSkinDetailsAsync(item.EntityId);
        item.TitleImageUrl = details.TitleImageUrl;
    }

    // 获取 主页图片[缓存保存] / 版本
    private static async Task GetFirstImageByCache(EntityQueryNetSkinItem item)
    {
        await GetFirstImage(item);
        // CacheManager.GetCacheImageUrl(item)：皮肤图片缓存（t14 移植中，暂省略）
    }

    /// <summary>
    /// 按名称搜索皮肤列表（逐条从详情页补全图片，过滤仍无图片的条目）。
    /// @param name 皮肤名称
    /// @param offset 偏移量
    /// @param pageSize 数量
    /// @return 皮肤列表
    /// </summary>
    public static async Task<EntityQueryNetSkinItem[]> GetSkinListByName(string name, int offset = 0, int pageSize = 10)
    {
        var result = NPFLauncher.GetFreeSkinByNameAsync(name, offset, pageSize).GetAwaiter().GetResult();

        var items = new List<EntityQueryNetSkinItem>();
        foreach (var item in result)
        {
            await GetFirstImageByCache(item);
            if (string.IsNullOrEmpty(item.TitleImageUrl))
            {
                continue;
            }

            items.Add(item);
        }

        return items.ToArray();
    }
}
