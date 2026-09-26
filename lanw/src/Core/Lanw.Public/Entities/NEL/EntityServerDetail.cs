using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using Lanw.Core.Utils.CodeTools;
using Lanw.Public.Manager;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails;
using Lanw.WPFLauncher.Protocol;

namespace Lanw.Public.Entities.NEL;

/// <summary>
/// 服务器详情聚合（由 Nirvana.Public.Entities.NEL.EntityServerDetail 移植，自研，t14）。
/// 并行拉取网络服详情与服务器地址后聚合为对外 JSON 形状。
/// </summary>
public class EntityServerDetail
{
    public EntityServerDetail(string id)
    {
        Exception? exception = null;
        var threads = new List<Thread>
        {
            new(() =>
            {
                try
                {
                    if (exception != null)
                    {
                        return;
                    }

                    var item = NPFLauncher.GetNetGameDetailByIdAsync(id).GetAwaiter().GetResult();
                    CacheManager.ClearCacheImage(item);
                    Set(item);
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }),
            new(() =>
            {
                try
                {
                    if (exception != null)
                    {
                        return;
                    }

                    Set(NPFLauncher.GetNetGameServerAddressAsync(id).GetAwaiter().GetResult());
                }
                catch (Exception e)
                {
                    exception = e;
                }
            })
        };
        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        if (exception != null)
        {
            throw exception;
        }
    }

    [JsonPropertyName("id")]
    [JsonInclude]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    [JsonInclude]
    public string? Name { get; set; }

    [JsonPropertyName("author")]
    [JsonInclude]
    public string? Author { get; set; }

    [JsonPropertyName("createdAt")]
    [JsonInclude]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("gameVersion")]
    [JsonInclude]
    public string? GameVersion { get; set; }

    [JsonPropertyName("address")]
    [JsonInclude]
    public string? Address { get; set; }

    [JsonPropertyName("fullDescription")]
    [JsonInclude]
    public string? FullDescription { get; set; }

    [JsonPropertyName("brief_image_urls")]
    [JsonInclude]
    public string[]? BriefImageUrls { get; set; }

    private void Set(EntityQueryNetGameDetailItem? data)
    {
        // 成功检测
        if (data == null)
        {
            throw new ErrorCodeException(ErrorCode.LogInNot);
        }

        Id = data.EntityId;
        Name = data.Name;
        Author = data.DeveloperName;
        // unix 时间戳 转换为 文本
        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(data.PublishTime).ToString("yyyy-MM-dd");
        GameVersion = "";
        foreach (var version in data.McVersionList)
        {
            GameVersion += version.Name + ", ";
        }

        // 删除最后一个逗号
        GameVersion = GameVersion.TrimEnd(',', ' ');
        FullDescription = data.DetailDescription;
        BriefImageUrls = data.BriefImageUrls;
    }

    private void Set(EntityNetGameServerAddress? data)
    {
        if (data == null)
        {
            throw new ErrorCodeException(ErrorCode.AddressError);
        }

        Address = data.Host;
        if (data.Port != 25565) Address += $":{data.Port}";
    }
}
