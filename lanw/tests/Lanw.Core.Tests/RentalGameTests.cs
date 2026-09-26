using System.Text.Json;
using Lanw.Core.Utils.CodeTools;
using Lanw.Public.Message;
using Lanw.WPFLauncher.Entities.WPFLauncher;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;

namespace Lanw.Core.Tests;

// 校验 Nirvana 租赁服后端 → Lanw 移植后的行为（实体 JSON 契约 / 消息编排逻辑，逻辑等价，自研命名）。
public class RentalGameTests
{
    // --- 枚举值契约 ---
    [Fact]
    public void RentalGame_Enums_MatchWireContract()
    {
        Assert.Equal(-1, (int)EnumServerStatus.None); // 0xFFFFFFFF
        Assert.Equal(0, (int)EnumServerStatus.ServerOff);
        Assert.Equal(1, (int)EnumServerStatus.ServerOn);
        Assert.Equal(9, (int)EnumServerStatus.DiscOverflow);
        Assert.Equal(0, (int)EnumVisibilityStatus.Public);
        Assert.Equal(1, (int)EnumVisibilityStatus.Friend);
        Assert.Equal(2, (int)EnumVisibilityStatus.Private);
        Assert.Equal(3, (int)EnumVisibilityStatus.Password);
    }

    // --- EntityRentalGame：列表项[普通信息] 反序列化 ---
    [Fact]
    public void EntityRentalGame_DeserializesFullPayload()
    {
        const string json = """
        {
            "entity_id": "4664453443934401593",
            "name": "租赁世界",
            "server_name": "rental-server",
            "visibility": 3,
            "has_pwd": "true",
            "server_type": "vanilla",
            "status": 1,
            "capacity": 40,
            "mc_version": "1.21.4",
            "owner_id": 9007199254740993,
            "player_count": 12,
            "image_url": "https://img.example/rental.png",
            "world_id": "world-1",
            "min_level": "0",
            "pvp": true,
            "like_num": 55,
            "icon_index": 2,
            "offset": "0",
            "brief_summary": null
        }
        """;

        var entity = JsonSerializer.Deserialize<EntityRentalGame>(json);

        Assert.NotNull(entity);
        Assert.Equal("4664453443934401593", entity.EntityId);
        Assert.Equal("租赁世界", entity.Name);
        Assert.Equal("rental-server", entity.ServerName);
        Assert.Equal(EnumVisibilityStatus.Password, entity.Visibility);
        Assert.Equal("true", entity.HasPassword);
        Assert.Equal("vanilla", entity.ServerType);
        Assert.Equal(EnumServerStatus.ServerOn, entity.Status);
        Assert.Equal(40u, entity.Capacity);
        Assert.Equal("1.21.4", entity.McVersion);
        Assert.Equal(9007199254740993L, entity.OwnerId); // 超出 int 范围，验证 long 契约
        Assert.Equal(12u, entity.PlayerCount);
        Assert.Equal("https://img.example/rental.png", entity.ImageUrl);
        Assert.Equal("world-1", entity.WorldId);
        Assert.Equal("0", entity.MinLevel);
        Assert.True(entity.IsPvpEnabled);
        Assert.Equal(55u, entity.LikeCount);
        Assert.Equal(2u, entity.IconIndex);
        Assert.Equal("0", entity.Offset);
        Assert.Null(entity.BriefSummary); // 普通信息 无该数据
    }

    // --- EntityRentalGameDetails：详细信息 反序列化 + 序列化 ---
    [Fact]
    public void EntityRentalGameDetails_RoundTripsWithSnakeCaseNames()
    {
        const string json = """
        {
            "entity_id": "4664453443934401593",
            "brief_summary": "生存租赁服",
            "mc_version": "1.21.4",
            "capacity": 40,
            "player_count": 12,
            "image_url": "https://img.example/rental.png",
            "server_type": "rental",
            "server_name": "rental-server"
        }
        """;

        var entity = JsonSerializer.Deserialize<EntityRentalGameDetails>(json);

        Assert.NotNull(entity);
        Assert.Equal("4664453443934401593", entity.EntityId);
        Assert.Equal("生存租赁服", entity.BriefSummary);
        Assert.Equal("1.21.4", entity.McVersion);
        Assert.Equal(40u, entity.Capacity);
        Assert.Equal(12u, entity.PlayerCount);
        Assert.Equal("rental", entity.ServerType);

        // 序列化保持 snake_case 契约
        var serialized = JsonSerializer.Serialize(entity);
        Assert.Contains("\"entity_id\":", serialized);
        Assert.Contains("\"brief_summary\":", serialized);
        Assert.Contains("\"mc_version\":", serialized);
        Assert.Contains("\"server_name\":", serialized);
        Assert.DoesNotContain("\"EntityId\":", serialized);
    }

    // --- EntityRentalGameServerAddress：三线接入地址 反序列化 ---
    [Fact]
    public void EntityRentalGameServerAddress_DeserializesIspLines()
    {
        const string json = """
        {
            "mcserver_host": "mc.example.com",
            "mcserver_port": 65535,
            "state": 1,
            "cmcc_mcserver_host": "cmcc.example.com",
            "cmcc_mcserver_port": 25561,
            "ctcc_mcserver_host": "ctcc.example.com",
            "ctcc_mcserver_port": 25562,
            "cucc_mcserver_host": "cucc.example.com",
            "cucc_mcserver_port": 25563,
            "isp_enable": true
        }
        """;

        var entity = JsonSerializer.Deserialize<EntityRentalGameServerAddress>(json);

        Assert.NotNull(entity);
        Assert.Equal("mc.example.com", entity.McServerHost);
        Assert.Equal((ushort)65535, entity.McServerPort); // ushort 上界
        Assert.Equal(EnumServerStatus.ServerOn, entity.State);
        Assert.Equal("cmcc.example.com", entity.CmccMcServerHost);
        Assert.Equal(25561, entity.CmccMcServerPort);
        Assert.Equal("ctcc.example.com", entity.CtccMcServerHost);
        Assert.Equal(25562, entity.CtccMcServerPort);
        Assert.Equal("cucc.example.com", entity.CuccMcServerHost);
        Assert.Equal(25563, entity.CuccMcServerPort);
        Assert.True(entity.IspEnable);
    }

    // --- 查询请求实体：序列化 snake_case 契约 ---
    [Fact]
    public void RentalGameQueryEntities_SerializeToExpectedJson()
    {
        Assert.Equal(
            """{"offset":5,"sort_type":1}""",
            JsonSerializer.Serialize(new EntityQueryRentalGame { Offset = 5, SortType = 1 }));

        Assert.Equal(
            """{"server_id":"s-001"}""",
            JsonSerializer.Serialize(new EntityQueryRentalGameDetail { ServerId = "s-001" }));

        Assert.Equal(
            """{"server_id":"s-001","pwd":"none"}""",
            JsonSerializer.Serialize(new EntityQueryRentalGameServerAddress { ServerId = "s-001", Password = "none" }));

        Assert.Equal(
            """{"server_id":"s-001","offset":0,"length":10}""",
            JsonSerializer.Serialize(new EntityQueryRentalGamePlayerList { ServerId = "s-001", Offset = 0, Length = 10 }));

        Assert.Equal(
            """{"server_id":"s-001","user_id":"u-100","name":"Alex","create_ts":555555,"is_online":false,"status":0}""",
            JsonSerializer.Serialize(new EntityAddRentalGameRole
            {
                ServerId = "s-001",
                UserId = "u-100",
                Name = "Alex",
                CreateTs = 555555,
                IsOnline = false,
                Status = 0
            }));
    }

    // --- EntityRentalGamePlayerList：玩家角色 反序列化 ---
    [Fact]
    public void EntityRentalGamePlayerList_DeserializesPlayer()
    {
        const string json = """
        {
            "entity_id": "4664453443934401594",
            "server_id": "4664453443934401593",
            "user_id": "4664453443934401590",
            "name": "Steve",
            "create_ts": 1735689600,
            "delete_ts": 0,
            "is_online": true
        }
        """;

        var entity = JsonSerializer.Deserialize<EntityRentalGamePlayerList>(json);

        Assert.NotNull(entity);
        Assert.Equal("4664453443934401594", entity.EntityId);
        Assert.Equal("4664453443934401593", entity.ServerId);
        Assert.Equal("4664453443934401590", entity.UserId);
        Assert.Equal("Steve", entity.Name);
        Assert.Equal(1735689600UL, entity.CreateTs); // ulong 契约
        Assert.Equal(0UL, entity.DeleteTs);
        Assert.True(entity.IsOnline);
    }

    // --- EntitiesWPFLauncher：租赁服列表包装 响应解析 ---
    [Fact]
    public void EntitiesWPFLauncher_WrapsRentalGameList()
    {
        const string json = """
        {
            "code": 0,
            "message": "ok",
            "entities": [
                { "entity_id": "r-1", "name": "R1", "server_name": "s1", "visibility": 0, "status": 1, "capacity": 10, "player_count": 3, "mc_version": "1.21.4", "owner_id": 1, "pvp": false },
                { "entity_id": "r-2", "name": "R2", "server_name": "s2", "visibility": 3, "status": 0, "capacity": 20, "player_count": 7, "mc_version": "1.20.1", "owner_id": 2, "pvp": true }
            ]
        }
        """;

        var wrapper = JsonSerializer.Deserialize<EntitiesWPFLauncher<EntityRentalGame>>(json);

        Assert.NotNull(wrapper);
        Assert.Equal(0, wrapper.Code);
        var entities = wrapper.SafeEntity();
        Assert.Equal(2, entities.Length);
        Assert.Equal("r-1", entities[0].EntityId);
        Assert.Equal("r-2", entities[1].EntityId);
        Assert.Equal(EnumVisibilityStatus.Password, entities[1].Visibility);
        Assert.Equal(EnumServerStatus.ServerOff, entities[1].Status);
    }

    // --- EntitiesWPFLauncher：entities 缺失时 SafeEntity 抛出 X19 异常 ---
    [Fact]
    public void EntitiesWPFLauncher_MissingEntities_ThrowsX19Exception()
    {
        var wrapper = JsonSerializer.Deserialize<EntitiesWPFLauncher<EntityRentalGame>>("""{"code":0,"message":"no entities"}""");

        Assert.NotNull(wrapper);
        Assert.Throws<Lanw.WPFLauncher.Entities.EntityX19Exception>(() => wrapper.SafeEntity());
    }

    // --- RentalGameMessage.SortServerList：按 PlayerCount 高到低 ---
    [Fact]
    public void RentalGameMessage_SortServerList_OrdersByPlayerCountDesc()
    {
        var original = RentalGameMessage.ServerList;
        try
        {
            RentalGameMessage.ServerList = new Dictionary<string, EntityRentalGameDetails>
            {
                ["a"] = new() { EntityId = "a", PlayerCount = 5 },
                ["b"] = new() { EntityId = "b", PlayerCount = 40 },
                ["c"] = new() { EntityId = "c", PlayerCount = 20 },
            };

            RentalGameMessage.SortServerList();

            Assert.Equal(new[] { "b", "c", "a" }, RentalGameMessage.ServerList.Keys.ToArray());
            Assert.Equal(new uint[] { 40, 20, 5 }, RentalGameMessage.ServerList.Values.Select(x => x.PlayerCount).ToArray());
        }
        finally
        {
            RentalGameMessage.ServerList = original; // 还原静态状态，避免污染其它用例
        }
    }

    // --- RentalGameMessage.ServerList：公开缓存字典语义（去重键） ---
    [Fact]
    public void RentalGameMessage_ServerList_IsKeyedCache()
    {
        var original = RentalGameMessage.ServerList;
        try
        {
            RentalGameMessage.ServerList = [];
            RentalGameMessage.ServerList["r-1"] = new() { EntityId = "r-1", PlayerCount = 1 };

            var exists = RentalGameMessage.ServerList.Any(item => item.Value.EntityId == "r-1");
            Assert.True(exists); // 与 AddServerList 去重判定一致
            Assert.Single(RentalGameMessage.ServerList);
        }
        finally
        {
            RentalGameMessage.ServerList = original;
        }
    }

    // --- EntityWPFLauncher：单实体响应 承载租赁服详细信息（GetRentalGameDetailsAsync 返回值形状） ---
    [Fact]
    public void EntityWPFLauncher_WrapsRentalGameDetails()
    {
        const string json = """
        {
            "code": 0,
            "message": "ok",
            "entity": {
                "entity_id": "4664453443934401593",
                "brief_summary": "生存租赁服",
                "mc_version": "1.21.4",
                "capacity": 40,
                "player_count": 12,
                "image_url": "https://img.example/rental.png",
                "server_type": "rental",
                "server_name": "rental-server"
            }
        }
        """;

        var wrapper = JsonSerializer.Deserialize<EntityWPFLauncher<EntityRentalGameDetails>>(json);

        Assert.NotNull(wrapper);
        Assert.Equal(0, wrapper.Code);
        Assert.Equal("ok", wrapper.Message);

        var details = wrapper.SafeEntity();
        Assert.Equal("4664453443934401593", details.EntityId);
        Assert.Equal("生存租赁服", details.BriefSummary);
        Assert.Equal(40u, details.Capacity);
        Assert.Equal(12u, details.PlayerCount);
        Assert.Equal("rental", details.ServerType);
    }

    // --- EntityWPFLauncher：单实体响应 承载租赁服连接地址（GetGameRentalAddressAsync 返回值形状） ---
    [Fact]
    public void EntityWPFLauncher_WrapsRentalServerAddress()
    {
        const string json = """
        {
            "code": 0,
            "message": "ok",
            "entity": {
                "mcserver_host": "mc.example.com",
                "mcserver_port": 25565,
                "state": 3,
                "cmcc_mcserver_host": "cmcc.example.com",
                "cmcc_mcserver_port": 25561,
                "ctcc_mcserver_host": "ctcc.example.com",
                "ctcc_mcserver_port": 25562,
                "cucc_mcserver_host": "cucc.example.com",
                "cucc_mcserver_port": 25563,
                "isp_enable": true
            }
        }
        """;

        var wrapper = JsonSerializer.Deserialize<EntityWPFLauncher<EntityRentalGameServerAddress>>(json);

        Assert.NotNull(wrapper);

        var address = wrapper.SafeEntity();
        Assert.Equal("mc.example.com", address.McServerHost);
        Assert.Equal((ushort)25565, address.McServerPort);
        Assert.Equal(EnumServerStatus.Opening, address.State); // 开服中
        Assert.Equal("cmcc.example.com", address.CmccMcServerHost);
        Assert.Equal("cucc.example.com", address.CuccMcServerHost);
        Assert.Equal(25563, address.CuccMcServerPort);
        Assert.True(address.IspEnable);
    }

    // --- EntityWPFLauncher：entity 缺失时 SafeEntity 抛出 X19 异常 ---
    [Fact]
    public void EntityWPFLauncher_MissingEntity_ThrowsX19Exception()
    {
        var wrapper = JsonSerializer.Deserialize<EntityWPFLauncher<EntityRentalGameDetails>>("""{"code":0,"message":"no entity"}""");

        Assert.NotNull(wrapper);
        Assert.Throws<Lanw.WPFLauncher.Entities.EntityX19Exception>(() => wrapper.SafeEntity());
    }

    // --- EntityWPFLauncher：服务端错误码（非 0）原样透出，供调用方判定失败 ---
    [Fact]
    public void EntityWPFLauncher_ErrorCode_IsExposedToCaller()
    {
        var wrapper = JsonSerializer.Deserialize<EntityWPFLauncher<EntityRentalGameDetails>>("""{"code":1001,"message":"server not found"}""");

        Assert.NotNull(wrapper);
        Assert.Equal(1001, wrapper.Code);
        Assert.Equal("server not found", wrapper.Message);
    }

    // --- ErrorCode 契约：租赁服失败路径使用的错误码 ---
    [Fact]
    public async Task RentalGame_ErrorCodes_Exist()
    {
        Assert.Equal(13, (int)ErrorCode.NotFound);
        Assert.Equal(20, (int)ErrorCode.NotFoundName);
        await Assert.ThrowsAsync<ErrorCodeException>(() => { throw new ErrorCodeException(ErrorCode.NotFoundName); });
    }
}
