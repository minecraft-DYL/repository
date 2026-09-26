using System.Text.Json;
using Lanw.Core.Entities.Login;
using Lanw.Core.Utils;
using Lanw.Public.Entities.NEL;
using Lanw.Public.Manager;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameCharacters;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails;

namespace Lanw.Core.Tests;

// 校验网络服（脱盒核心）后端移植：NetGame 实体解析 + RunningProxy/EntityProxyBase + 图片缓存路径。
public class NetGamePortTests
{
    // --- EntityNetGameItem / EntityMcVersion ---

    [Fact]
    public void EntityNetGameItem_ShouldParseEntityIdAndSummary()
    {
        var item = JsonSerializer.Deserialize<EntityNetGameItem>("""
        {
          "entity_id": "4678000000000000000",
          "brief_summary": "生存服的简介",
          "name": "示例生存服"
        }
        """)!;

        Assert.Equal("4678000000000000000", item.EntityId);
        Assert.Equal("生存服的简介", item.BriefSummary);
        Assert.Equal("示例生存服", item.Name);
        // title_image_url / version 来自详细信息，缺省为空串
        Assert.Equal(string.Empty, item.TitleImageUrl);
        Assert.Equal(string.Empty, item.Version);
    }

    [Fact]
    public void EntityNetGameItem_OnlineCount_ShouldAcceptNumberAndString()
    {
        var fromNumber = JsonSerializer.Deserialize<EntityNetGameItem>("""{"online_count":1234}""")!;
        Assert.Equal("1234", fromNumber.OnlineCount);

        // 网易接口的在线人数偶尔返回 “1.2万” 之类的文本
        var fromText = JsonSerializer.Deserialize<EntityNetGameItem>("""{"online_count":"1.2万"}""")!;
        Assert.Equal("1.2万", fromText.OnlineCount);
    }

    [Fact]
    public void EntityNetGameItem_TitleImageSafe_ShouldAcceptRemoteAndLocalUrl()
    {
        var item = new EntityNetGameItem();

        Assert.False(item.TitleImageSafe());

        item.TitleImageUrl = "https://x19.res.netease.com/a.png";
        Assert.True(item.TitleImageSafe());

        item.TitleImageUrl = "/image/net/4678000000000000000.png";
        Assert.True(item.TitleImageSafe());
    }

    [Fact]
    public void EntityMcVersion_ShouldParseIdAndName()
    {
        var version = JsonSerializer.Deserialize<EntityMcVersion>("""{"mcversionid":17,"name":"1.20.1"}""")!;

        Assert.Equal(17, version.McVersionId);
        Assert.Equal("1.20.1", version.Name);
    }

    // --- EntityNetGameServerAddress ---

    [Fact]
    public void EntityNetGameServerAddress_ShouldParseHostAndPort()
    {
        var address = JsonSerializer.Deserialize<EntityNetGameServerAddress>("""{"ip":"mc.example.com","port":25565}""")!;

        Assert.Equal("mc.example.com", address.Host);
        Assert.Equal(25565, address.Port);
    }

    [Fact]
    public void EntityNetGameServerAddress_ShouldHonorCustomPort()
    {
        var address = JsonSerializer.Deserialize<EntityNetGameServerAddress>("""{"ip":"10.0.0.1","port":19132}""")!;

        Assert.Equal("10.0.0.1", address.Host);
        Assert.Equal(19132, address.Port);
        // EntityServerDetail 仅在非默认端口时拼接 ":端口"
        Assert.True(address.Port != 25565);
    }

    // --- EntityQueryNetGameDetailItem / EntityDetailsVideo ---

    [Fact]
    public void EntityQueryNetGameDetailItem_ShouldParseNestedVersionsAndVideos()
    {
        var item = JsonSerializer.Deserialize<EntityQueryNetGameDetailItem>("""
        {
          "name": "示例网络服",
          "entity_id": "1234567890",
          "brief_image_urls": ["https://img/1.png", "https://img/2.png"],
          "detail_description": "详细介绍",
          "developer_name": "开发者",
          "developer_urs": "https://urs",
          "publish_time": 1700000000,
          "video_info_list": [{ "cover": "https://img/cover.png", "size": 42, "url": "https://video/1.mp4" }],
          "mc_version_list": [{ "mcversionid": 1, "name": "1.20" }, { "mcversionid": 2, "name": "1.19.2" }],
          "server_address": "mc.example.com",
          "server_port": 25565
        }
        """)!;

        Assert.Equal("示例网络服", item.Name);
        Assert.Equal("1234567890", item.EntityId);
        Assert.Equal(new[] { "https://img/1.png", "https://img/2.png" }, item.BriefImageUrls);
        Assert.Equal("详细介绍", item.DetailDescription);
        Assert.Equal("开发者", item.DeveloperName);
        Assert.Equal(1700000000, item.PublishTime);
        Assert.Equal("mc.example.com", item.ServerAddress);
        Assert.Equal(25565, item.ServerPort);

        var video = Assert.Single(item.VideoInfoList);
        Assert.Equal("https://img/cover.png", video.Cover);
        Assert.Equal(42, video.Size);
        Assert.Equal("https://video/1.mp4", video.Url);

        // EntityServerDetail.GameVersion 的拼接口径：逗号+空格连接，去尾
        Assert.Equal("1.20, 1.19.2", string.Join(", ", item.McVersionList.Select(version => version.Name)));
        Assert.Equal("2023-11-14", DateTimeOffset.FromUnixTimeSeconds(item.PublishTime).ToString("yyyy-MM-dd"));
    }

    // --- 请求体字段名（X19 接口契约） ---

    [Fact]
    public void EntityQueryNetGameDetailRequest_ShouldSerializeItemId()
    {
        var json = JsonSerializer.Serialize(new EntityQueryNetGameDetailRequest { ItemId = "42" });

        Assert.Equal("""{"item_id":"42"}""", json);
    }

    [Fact]
    public void EntityNetGameRequest_ShouldSerializeNetGameQueryBody()
    {
        var json = JsonSerializer.Serialize(new EntityNetGameRequest
        {
            AvailableMcVersions = [],
            ItemType = 1,
            Length = 20,
            Offset = 0,
            MasterTypeId = "2",
            SecondaryTypeId = ""
        });

        Assert.Equal("""{"available_mc_versions":[],"item_type":1,"length":20,"offset":0,"master_type_id":"2","secondary_type_id":""}""", json);
    }

    // --- EntityGameCharacter / EntityQueryGameCharacters ---

    [Fact]
    public void EntityGameCharacter_ShouldUseFantnelDefaults()
    {
        var character = JsonSerializer.Deserialize<EntityGameCharacter>("""{"game_id":"1","name":"Steve"}""")!;

        Assert.Equal("1", character.GameId);
        Assert.Equal("Steve", character.Name);
        Assert.Equal(2, character.GameType);
        Assert.Equal(555555, character.CreateTime);
    }

    [Fact]
    public void EntityQueryGameCharacters_ShouldSerializeGameAndUser()
    {
        var json = JsonSerializer.Serialize(new EntityQueryGameCharacters { GameId = "1", UserId = "u1" });

        Assert.Equal("""{"offset":0,"length":10,"user_id":"u1","game_id":"1","game_type":"2"}""", json);
    }

    // --- EntityProxyBase / RunningProxy ---

    [Fact]
    public void RunningProxy_ShouldExposeLocalAddressAndNickName()
    {
        var proxy = new RunningProxy(true, "127.0.0.1", 25565, "Steve", "示例网络服")
        {
            Account = new EntityUserInfo { UserId = "u1", Token = "t1" },
            ServerId = "1234567890"
        };

        Assert.True(proxy.IsRental);
        Assert.Equal("127.0.0.1", proxy.LocalAddress);
        Assert.Equal(25565, proxy.LocalPort);
        Assert.Equal("示例网络服", proxy.ServerName);
        Assert.Equal("Steve", proxy.GetNickName());
    }

    [Fact]
    public void RunningProxy_ShouldSerializeFantnelJsonShape()
    {
        var proxy = new RunningProxy(false, "127.0.0.1", 25566, "Steve", "示例网络服")
        {
            Account = new EntityUserInfo { UserId = "u1", Token = "t1" }
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(proxy));
        var root = document.RootElement;

        Assert.Equal("127.0.0.1", root.GetProperty("local_address").GetString());
        Assert.Equal(25566, root.GetProperty("local_port").GetInt32());
        Assert.Equal("示例网络服", root.GetProperty("server_name").GetString());
        Assert.False(root.GetProperty("is_rental").GetBoolean());
        Assert.Equal("Steve", root.GetProperty("nick_name").GetString());
    }

    [Fact]
    public void RunningProxy_Shutdown_ShouldInvokeCallbackAndBeSafeByDefault()
    {
        var shutdownCount = 0;
        var proxy = new RunningProxy(false, "127.0.0.1", 25565, "Steve", "示例网络服", () => shutdownCount++)
        {
            Account = new EntityUserInfo { UserId = "u1", Token = "t1" }
        };

        proxy.Shutdown();
        proxy.Shutdown();
        Assert.Equal(2, shutdownCount);

        // 未提供关闭回调时不应抛异常
        new RunningProxy(false, "127.0.0.1", 25565, "Alex", "示例网络服")
        {
            Account = new EntityUserInfo { UserId = "u1", Token = "t1" }
        }.Shutdown();
    }

    [Fact]
    public void EntityProxyBase_Equals_ShouldMatchSameUserServerNickName()
    {
        var proxy = new RunningProxy(false, "127.0.0.1", 25565, "Steve", "示例网络服")
        {
            Account = new EntityUserInfo { UserId = "u1", Token = "t1" },
            ServerId = "s1"
        };

        // 同用户 + 同服务器 + 同昵称 → 同一个代理
        Assert.True(proxy.Equals(new EntityAccount { UserId = "u1", Token = "t1" }, "s1", "Steve"));

        // 昵称或服务器不同 → 不是同一个代理
        Assert.False(proxy.Equals(new EntityAccount { UserId = "u1", Token = "t1" }, "s1", "Alex"));
        Assert.False(proxy.Equals(new EntityAccount { UserId = "u1", Token = "t1" }, "s2", "Steve"));
        Assert.False(proxy.Equals(new EntityAccount { UserId = "u2", Token = "t1" }, "s1", "Steve"));

        // 同用户但 Token 不同 → 视为过期代理
        Assert.True(proxy.Equals(new EntityAccount { UserId = "u1", Token = "t2" }, null, null));

        // 完全无关的账号且信息不匹配 → 不是同一个代理
        Assert.False(proxy.Equals(new EntityAccount { UserId = "u2", Token = "t2" }, null, null));
    }

    // --- CacheManager / 图片缓存路径 ---

    [Fact]
    public void PathUtil_CacheImagePath_ShouldPointToResourcesStaticImage()
    {
        Assert.Equal(Path.Combine(PathUtil.ResourcePath, "static", "image"), PathUtil.CacheImagePath);
        Assert.Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources"), PathUtil.ResourcePath);
    }

    [Fact]
    public void CacheManager_GetCacheImageUrl_ShouldSwitchToLocalPathOnlyWhenCached()
    {
        var entityId = "t14cache" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(PathUtil.CacheImagePath, "net");
        var cacheFile = Path.Combine(directory, entityId + ".png");
        Directory.CreateDirectory(directory);

        try
        {
            var item = new EntityNetGameItem { EntityId = entityId, TitleImageUrl = "https://img/remote.png" };

            // 未缓存：保持远端地址，等待 DownloadCacheImage 落盘
            CacheManager.GetCacheImageUrl(item);
            Assert.Equal("https://img/remote.png", item.TitleImageUrl);

            // 已缓存：改用本地路径，避免重复请求
            File.WriteAllBytes(cacheFile, [1, 2, 3]);
            CacheManager.GetCacheImageUrl(item);
            Assert.Equal("/image/net/" + entityId + ".png", item.TitleImageUrl);
        }
        finally
        {
            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
            }
        }
    }

    [Fact]
    public void CacheManager_GetCacheImageUrl_ShouldBeIdempotentForSameEntity()
    {
        var entityId = "t14cache" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(PathUtil.CacheImagePath, "net");
        var cacheFile = Path.Combine(directory, entityId + ".png");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllBytes(cacheFile, [1]);
            var first = new EntityNetGameItem { EntityId = entityId, TitleImageUrl = "https://img/remote.png" };
            var second = new EntityNetGameItem { EntityId = entityId, TitleImageUrl = "https://img/remote.png" };

            CacheManager.GetCacheImageUrl(first);
            CacheManager.GetCacheImageUrl(second);

            Assert.Equal("/image/net/" + entityId + ".png", first.TitleImageUrl);
            Assert.Equal(first.TitleImageUrl, second.TitleImageUrl);
        }
        finally
        {
            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
            }
        }
    }
}
