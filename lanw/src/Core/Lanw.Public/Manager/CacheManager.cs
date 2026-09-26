using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lanw.Core.Utils;
using Lanw.Public.Message;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails;
using Serilog;

namespace Lanw.Public.Manager;

/// <summary>
/// 服务器/图片缓存管理（由 Nirvana.Public.Manager.CacheManager 的网络服部分移植，自研，t14）。
/// 图片缓存到本地（resources/static/image）避免重复请求；皮肤/租赁服缓存随 t15/t16 各自的 Message 落地后补充。
/// </summary>
public class CacheManager
{
    // 缓存图片[待下载]
    private static readonly List<EntityNetGameItem> CacheNet = [];
    private static readonly Lock CacheImageLock = new();

    // 服务器列表缓存
    private static readonly Lock CacheServerLock = new();

    // 图片下载客户端（原参考 CacheManager 经 Nirvana.Game.Launcher.Utils.DownloadUtil 下载；
    // 该模块由 t18 移植为 Lanw.Game.Launcher，此处以内置 HttpClient 等价实现，成功落盘返回 true）
    private static readonly HttpClient DownloadHttpClient = new();

    // 服务器列表缓存（预热；参考版含 Rental/Skin 预热，随 t15/t16 的 Message 落地后补充）
    public static void CacheServer()
    {
        _ = Task.Run(() =>
        {
            lock (CacheServerLock)
            {
                try
                {
                    if (ServersGameMessage.ServerList.Count < 20)
                    {
                        Log.Information("正在开始缓存 Net 服务器列表");
                        ServersGameMessage.GetServerListTo(0, 50);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("顶缓存 Net 出错 : {0}", e.Message);
                }
            }
        });
    }

    // 缓存图片
    public static void GetCacheImageUrl(EntityNetGameItem item)
    {
        lock (CacheImageLock)
        {
            var filePath = GetCacheImagePath(item.EntityId, "net");
            if (File.Exists(filePath))
            {
                item.TitleImageUrl = "/image/net/" + item.EntityId + ".png";
                return;
            }

            if (CacheNet.Any(cache => cache.EntityId == item.EntityId))
            {
                return;
            }

            CacheNet.Add(item);
        }
    }

    // 清理缓存图片
    private static void ClearCacheImageA(EntityQueryNetGameDetailItem item)
    {
        try
        {
            lock (CacheImageLock)
            {
                File.Delete(GetCacheImagePath(item.EntityId, "net"));
                foreach (var server in ServersGameMessage.ServerList.Where(server => server.EntityId == item.EntityId))
                {
                    server.TitleImageUrl = item.BriefImageUrls[0];
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("清理缓存图片 {0} 失败 : {1}", item.EntityId, e.Message);
        }
    }

    // 清理缓存图片
    public static void ClearCacheImage(EntityQueryNetGameDetailItem item)
    {
        _ = Task.Run(() => ClearCacheImageA(item));
    }

    // 获取缓存图片路径
    private static string GetCacheImagePath(string entityId, string name)
    {
        return Path.Combine(PathUtil.CacheImagePath, name, entityId + ".png");
    }

    // 缓存图片下载
    public static void DownloadCacheImage()
    {
        _ = Task.Run(() =>
        {
            lock (CacheImageLock)
            {
                try
                {
                    foreach (var item in CacheNet.ToArray())
                    {
                        CacheNet.Remove(item);
                        _ = DownloadCacheImage(item);
                        // 请求速限制 1 秒 / 12次 ≈ 0.083
                        Thread.Sleep(83);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("缓存图片下载失败 : {0}", e.Message);
                }
            }
        });
    }

    // 缓存图片下载
    private static async Task DownloadCacheImage(EntityNetGameItem item)
    {
        if (string.IsNullOrEmpty(item.TitleImageUrl))
        {
            return;
        }

        // 下载图片
        var filePath = GetCacheImagePath(item.EntityId, "net");
        var tmpPath = filePath + ".tmp";
        if (await DownloadAsync(item.TitleImageUrl, tmpPath))
        {
            File.Move(tmpPath, filePath);
            item.TitleImageUrl = "/image/net/" + item.EntityId + ".png";
        }
        else
        {
            Log.Error("缓存 Net 图片 {0} 失败", item.EntityId);
        }
    }

    // 下载文件到本地（原参考 Nirvana.Game.Launcher.Utils.DownloadUtil.DownloadAsync 的最小等价实现）
    private static async Task<bool> DownloadAsync(string url, string destinationPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = await DownloadHttpClient.GetStreamAsync(url);
            await using var file = File.Create(destinationPath);
            await stream.CopyToAsync(file);
            return true;
        }
        catch (Exception e)
        {
            Log.Error("下载 {0} 失败 : {1}", url, e.Message);
            return false;
        }
    }
}
