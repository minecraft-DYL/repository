using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lanw.Public.Entities.NEL;
using Lanw.Public.Message;
using Lanw.WPFLauncher.Entities.WPFLauncher;
using Lanw.WPFLauncher.Entities.WPFLauncher.Minecraft;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameLaunch.Texture;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;
using Lanw.WPFLauncher.Protocol;

namespace Lanw.Core.Tests;

// 校验皮肤模块移植（Nirvana.WPFLauncher 皮肤/贴图实体与端点请求 → Lanw.WPFLauncher，
// Nirvana.Public.SkinMessage / EntitySkinDetail → Lanw.Public）后的行为（逻辑等价，自研命名）。
// 全部用例不触网：仅构造实体/请求做线格式校验，SkinMessage 仅测进程内缓存分页。
public class SkinPortTests
{
    private static EntityQueryNetSkinItem MkSkin(string id, string image = "", string name = "皮肤")
    {
        return new EntityQueryNetSkinItem
        {
            EntityId = id,
            Name = name,
            DeveloperName = "dev",
            BriefSummary = "sum",
            PublishTime = 1710000000L,
            DownloadNum = 12345L,
            LikeNum = 67,
            TitleImageUrl = image
        };
    }

    // --- 皮肤列表实体（EntityQueryNetSkinItem） ---
    [Fact]
    public void QueryNetSkinItem_Serializes_WithWireNames()
    {
        var item = MkSkin("4664453443934401593", "https://x19.fp.ps.netease.com/file/a", "非常可爱");

        var json = JsonSerializer.Serialize(item, NPFLauncher.DefaultOptions);

        Assert.Contains("\"entity_id\":\"4664453443934401593\"", json);
        Assert.Contains("\"name\":\"非常可爱\"", json); // UnsafeRelaxedJsonEscaping：中文不转义
        Assert.Contains("\"developer_name\":\"dev\"", json);
        Assert.Contains("\"brief_summary\":\"sum\"", json);
        Assert.Contains("\"publish_time\":1710000000", json);
        Assert.Contains("\"download_num\":12345", json);
        Assert.Contains("\"like_num\":67", json);
        Assert.Contains("\"title_image_url\":\"https://x19.fp.ps.netease.com/file/a\"", json);

        // 反序列化还原（响应解析）
        var back = JsonSerializer.Deserialize<EntityQueryNetSkinItem>(json)!;
        Assert.Equal("4664453443934401593", back.EntityId);
        Assert.Equal("非常可爱", back.Name);
        Assert.Equal(1710000000L, back.PublishTime);
        Assert.Equal(12345L, back.DownloadNum);
        Assert.Equal(67, back.LikeNum);
    }

    [Fact]
    public void QueryNetSkinItem_TitleImageSafe_DetectsUsableUrl()
    {
        Assert.True(MkSkin("a", "https://x19.fp.ps.netease.com/file/a").TitleImageSafe());
        Assert.True(MkSkin("b", "/image/skin/b.png").TitleImageSafe());
        Assert.False(MkSkin("c").TitleImageSafe());
        Assert.False(MkSkin("d", "ftp://example.com/d.png").TitleImageSafe());
    }

    // --- 免费皮肤列表请求（EntityFreeSkinListRequest） ---
    [Fact]
    public void FreeSkinListRequest_Serializes_WithWireNames()
    {
        var req = new EntityFreeSkinListRequest
        {
            IsHas = true,
            ItemType = 2,
            Length = 20,
            MasterTypeId = 10,
            Offset = 0,
            PriceType = 3,
            SecondaryTypeId = 31
        };

        var json = JsonSerializer.Serialize(req, NPFLauncher.DefaultOptions);

        Assert.Contains("\"is_has\":true", json);
        Assert.Contains("\"item_type\":2", json);
        Assert.Contains("\"length\":20", json);
        Assert.Contains("\"master_type_id\":10", json);
        Assert.Contains("\"offset\":0", json);
        Assert.Contains("\"price_type\":3", json);
        Assert.Contains("\"secondary_type_id\":31", json);
    }

    // --- 按名称查询请求（EntityQuerySkinByNameRequest） ---
    [Fact]
    public void QuerySkinByNameRequest_Serializes_WithWireNames()
    {
        var req = new EntityQuerySkinByNameRequest
        {
            IsHas = true,
            IsSync = 0,
            ItemType = 2,
            Keyword = "非常可爱",
            Length = 10,
            MasterTypeId = 10,
            Offset = 0,
            PriceType = 3,
            SecondaryTypeId = "31",
            SortType = 1,
            Year = 0
        };

        var json = JsonSerializer.Serialize(req, NPFLauncher.DefaultOptions);

        Assert.Contains("\"is_has\":true", json);
        Assert.Contains("\"is_sync\":0", json);
        Assert.Contains("\"item_type\":2", json);
        Assert.Contains("\"keyword\":\"非常可爱\"", json);
        Assert.Contains("\"length\":10", json);
        Assert.Contains("\"master_type_id\":10", json);
        Assert.Contains("\"offset\":0", json);
        Assert.Contains("\"price_type\":3", json);
        Assert.Contains("\"secondary_type_id\":\"31\"", json);
        Assert.Contains("\"sort_type\":1", json);
        Assert.Contains("\"year\":0", json);
    }

    // --- 皮肤详情批量查询请求（EntitySkinDetailsRequest） ---
    [Fact]
    public void SkinDetailsRequest_Serializes_WithWireNames()
    {
        var req = new EntitySkinDetailsRequest
        {
            ChannelId = 11,
            EntityIds = ["A", "B"],
            IsHas = true,
            WithPrice = true,
            WithTitleImage = true
        };

        var json = JsonSerializer.Serialize(req, NPFLauncher.DefaultOptions);

        Assert.Contains("\"channel_id\":11", json);
        Assert.Contains("\"entity_ids\":[\"A\",\"B\"]", json);
        Assert.Contains("\"is_has\":true", json);
        Assert.Contains("\"with_price\":true", json);
        Assert.Contains("\"with_title_image\":true", json);
    }

    // --- 皮肤应用设置（EntitySkinSettings，/user-game-skin-multi skin_settings 元素） ---
    [Fact]
    public void SkinSettings_Serializes_WithWireNames()
    {
        var settings = new EntitySkinSettings
        {
            ClientType = "java",
            GameType = 2,
            SkinId = "S1",
            SkinMode = 0,
            SkinType = 31
        };

        var json = JsonSerializer.Serialize(settings, NPFLauncher.DefaultOptions);

        Assert.Contains("\"client_type\":\"java\"", json);
        Assert.Contains("\"game_type\":2", json);
        Assert.Contains("\"skin_id\":\"S1\"", json);
        Assert.Contains("\"skin_mode\":0", json);
        Assert.Contains("\"skin_type\":31", json);
    }

    // --- 贴图枚举值与参考一致 ---
    [Fact]
    public void TextureEnums_MatchReferenceValues()
    {
        Assert.Equal(-1, (int)EnumGType.None);
        Assert.Equal(1, (int)EnumGType.SingleGame);
        Assert.Equal(2, (int)EnumGType.NetGame);
        Assert.Equal(7, (int)EnumGType.McGame);
        Assert.Equal(8, (int)EnumGType.ServerGame);
        Assert.Equal(9, (int)EnumGType.LanGame);
        Assert.Equal(10, (int)EnumGType.OnlineLobbyGame);

        Assert.Equal(31, (int)EnumTextureType.SKIN);
        Assert.Equal(41, (int)EnumTextureType.FOUR_DIMENSIONAL_SKIN);
        Assert.Equal(42, (int)EnumTextureType.SPECIAL_SKIN);

        Assert.Equal(0, (int)EnumGameClientType.All);
        Assert.Equal(1, (int)EnumGameClientType.Java);
        Assert.Equal(2, (int)EnumGameClientType.Cpp);
    }

    // --- 用户游戏贴图（EntityUserGameTexture）：响应实体，枚举以字符串传输 ---
    [Fact]
    public void UserGameTexture_SerializesEnums_AsStrings()
    {
        var texture = new EntityUserGameTexture
        {
            EntityId = "E1",
            GameType = EnumGType.NetGame,
            SkinType = EnumTextureType.SKIN,
            SkinId = "S1",
            SkinMode = 0,
            ClientType = EnumGameClientType.Java
        };

        var json = JsonSerializer.Serialize(texture, NPFLauncher.DefaultOptions);

        Assert.Contains("\"entity_id\":\"E1\"", json);
        Assert.Contains("\"game_type\":\"NetGame\"", json);
        Assert.Contains("\"skin_type\":\"SKIN\"", json);
        Assert.Contains("\"skin_id\":\"S1\"", json);
        Assert.Contains("\"skin_mode\":0", json);
        Assert.Contains("\"client_type\":\"Java\"", json);

        // 反序列化（服务端响应解析）
        var back = JsonSerializer.Deserialize<EntityUserGameTexture>("""{"entity_id":"E2","game_type":"ServerGame","skin_type":"FOUR_DIMENSIONAL_SKIN","skin_id":"S2","skin_mode":1,"client_type":"Cpp"}""")!;
        Assert.Equal("E2", back.EntityId);
        Assert.Equal(EnumGType.ServerGame, back.GameType);
        Assert.Equal(EnumTextureType.FOUR_DIMENSIONAL_SKIN, back.SkinType);
        Assert.Equal("S2", back.SkinId);
        Assert.Equal(1, back.SkinMode);
        Assert.Equal(EnumGameClientType.Cpp, back.ClientType);
    }

    // --- 用户游戏贴图查询请求（EntityUserGameTextureRequest）：ClientType 无转换器（数字传输，与参考一致） ---
    [Fact]
    public void UserGameTextureRequest_Serializes_WithWireNames()
    {
        var req = new EntityUserGameTextureRequest
        {
            UserId = "U1",
            GameType = "2",
            ClientType = EnumGameClientType.Java
        };

        var json = JsonSerializer.Serialize(req, NPFLauncher.DefaultOptions);

        Assert.Contains("\"user_id\":\"U1\"", json);
        Assert.Contains("\"game_type\":\"2\"", json);
        Assert.Contains("\"client_type\":1", json);
    }

    // --- 上传本地皮肤请求（lanw 自研扩展：参考源无此端点） ---
    [Fact]
    public void UploadSkinRequest_Serializes_WithWireNames()
    {
        var req = new EntityUploadSkinRequest
        {
            File = "aGVsbG8=",
            FileName = "myskin.png",
            ClientType = EnumGameClientType.Java,
            SkinType = EnumTextureType.SKIN,
            SkinMode = 0
        };

        var json = JsonSerializer.Serialize(req, NPFLauncher.DefaultOptions);

        Assert.Contains("\"file\":\"aGVsbG8=\"", json);
        Assert.Contains("\"file_name\":\"myskin.png\"", json);
        Assert.Contains("\"client_type\":\"Java\"", json);
        Assert.Contains("\"skin_type\":\"SKIN\"", json);
        Assert.Contains("\"skin_mode\":0", json);
    }

    // --- EntitySkinDetail：构造会触网，反射校验字段线格式 ---
    [Fact]
    public void EntitySkinDetail_WireNames_MatchReference()
    {
        var type = typeof(EntitySkinDetail);
        Assert.Equal("entity_id", WireName(type, nameof(EntitySkinDetail.EntityId)));
        Assert.Equal("brief_summary", WireName(type, nameof(EntitySkinDetail.BriefSummary)));
        Assert.Equal("name", WireName(type, nameof(EntitySkinDetail.Name)));
        Assert.Equal("title_image_url", WireName(type, nameof(EntitySkinDetail.TitleImageUrl)));
        Assert.Equal("like_num", WireName(type, nameof(EntitySkinDetail.LikeNum)));
        Assert.Equal("developer_name", WireName(type, nameof(EntitySkinDetail.DeveloperName)));
        Assert.Equal("publish_time", WireName(type, nameof(EntitySkinDetail.PublishTime)));
        Assert.Equal("download_num", WireName(type, nameof(EntitySkinDetail.DownloadNum)));
        return;

        static string WireName(Type type, string property)
        {
            var attribute = type.GetProperty(property)!.GetCustomAttribute<JsonPropertyNameAttribute>();
            return attribute!.Name;
        }
    }

    // --- SkinMessage：进程内皮肤列表缓存分页（缓存足量时不触网） ---
    [Fact]
    public async Task SkinMessage_GetSkinList_ServesCachedPages_WithoutNetwork()
    {
        var backup = SkinMessage.SkinList.ToArray();
        try
        {
            SkinMessage.SkinList.Clear();
            SkinMessage.SkinList.Add(MkSkin("A", "https://x19.fp.ps.netease.com/file/a"));
            SkinMessage.SkinList.Add(MkSkin("B", "https://x19.fp.ps.netease.com/file/b"));

            // 缓存足量：直接命中进程内分页；URL 均安全 → safeImage=true 也不触发详情补图
            var page1 = await SkinMessage.GetSkinList(0, 1);
            var page2 = await SkinMessage.GetSkinList(1, 1);
            var pageAll = await SkinMessage.GetSkinList(0, 2, safeImage: false);

            Assert.Single(page1);
            Assert.Equal("A", page1[0].EntityId);
            Assert.Single(page2);
            Assert.Equal("B", page2[0].EntityId);
            Assert.Equal(2, pageAll.Length);
        }
        finally
        {
            SkinMessage.SkinList.Clear();
            foreach (var item in backup)
            {
                SkinMessage.SkinList.Add(item);
            }
        }
    }
}
