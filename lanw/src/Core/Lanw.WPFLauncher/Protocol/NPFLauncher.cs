using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Lanw.Core.Manager;
using Lanw.Core.Utils.CodeTools;
using Lanw.WPFLauncher.Entities;
using Lanw.WPFLauncher.Entities.WPFLauncher;
using Lanw.WPFLauncher.Entities.WPFLauncher.Login;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameCharacters;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameSkin;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;
using Lanw.WPFLauncher.Http;
using Lanw.WPFLauncher.Utils;
using Serilog;

namespace Lanw.WPFLauncher.Protocol;

// ReSharper disable once InconsistentNaming
/// <summary>
/// X19 启动器协议（由 Nirvana.WPFLauncher.Protocol.NPFLauncher 移植，自研）。
/// 含登录认证相关方法（LoginWithCookie / 获取 OTP / AuthenticationOtp）
/// 与网络服方法（服务器列表 / 详情 / 服务器地址 / 游戏角色，t14 移植）
/// 与租赁服方法（租赁服列表 / 详情 / 地址 / 玩家角色，t16 移植）；
/// 皮肤/贴图方法（列表 / 详情 / 设置 / 查询 / 上传，t15 移植）见 NPFLauncher.Skin.cs 分部文件；
/// 模组等其余方法留待后续对应功能任务移植。
/// </summary>
public static partial class NPFLauncher
{
    private static readonly MgbSdk Sdk = new("x19");

    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 使用 Cookie 登录
    /// @param cookie Cookie 请求
    /// @return 登录成功后的用户信息
    /// </summary>
    public static EntityAuthenticationOtp LoginWithCookie(string cookie)
    {
        return LoginWithCookieAsync(cookie).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 使用 Cookie 登录
    /// @param cookie Cookie 请求
    /// @return 登录成功后的用户信息
    /// </summary>
    private static async Task<EntityAuthenticationOtp> LoginWithCookieAsync(string cookie)
    {
        EntityX19CookieRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<EntityX19CookieRequest>(cookie);
        }
        catch
        {
            req = new EntityX19CookieRequest { Json = cookie };
        }

        return req == null ? throw new ErrorCodeException() : await LoginWithCookieAsync(req);
    }

    /// <summary>
    /// 使用 Cookie 登录
    /// @param cookie Cookie 数据
    /// @return 登录成功后的用户信息
    /// </summary>
    private static async Task<EntityAuthenticationOtp> LoginWithCookieAsync(EntityX19CookieRequest cookie)
    {
        var entity = JsonSerializer.Deserialize<EntityX19Cookie>(cookie.Json);

        if (entity is not { LoginChannel: "netease" })
        {
            await Sdk.AuthSession(cookie.Json);
        }

        Log.Information("Login with Cookie...");
        var otp = await LoginOtpAsync(cookie);
        if (otp == null)
        {
            throw new ErrorCodeException(ErrorCode.LoginError);
        }

        return await AuthenticationOtpAsync(cookie, otp);
    }

    /// <summary>
    /// 获取登录 OTP
    /// @param cookieRequest Cookie 数据
    /// @return 登录 OTP
    /// </summary>
    private static async Task<EntityLoginOtp?> LoginOtpAsync(EntityX19CookieRequest cookieRequest)
    {
        var entity = await X19Extensions.Core.ApiAsync<EntityWPFLauncher<EntityLoginOtp>>("/login-otp", cookieRequest);
        if (entity == null)
        {
            throw new Exception("Failed to deserialize: login-otp");
        }

        return entity.Code != 0 ? throw new Exception(entity.Message) : entity.SafeEntity();
    }

    /// <summary>
    /// 使用 OTP 登录
    /// @param cookieRequest Cookie 数据
    /// @param otp 登录 OTP
    /// @return 登录成功后的用户信息
    /// </summary>
    private static async Task<EntityAuthenticationOtp> AuthenticationOtpAsync(EntityX19CookieRequest cookieRequest, EntityLoginOtp otp)
    {
        var entityX19Cookie = JsonSerializer.Deserialize<EntityX19Cookie>(cookieRequest.Json);
        if (entityX19Cookie == null)
        {
            throw new ErrorCodeException(ErrorCode.LoginError);
        }

        var upper = StringGenerator.GenerateHexString(4).ToUpper();
        var authenticationDetail = new EntityAuthenticationDetail
        {
            Udid = "0000000000000000" + upper,
            AppVersion = X19.GameVersion,
            PayChannel = entityX19Cookie.AppChannel,
            Disk = upper
        };
        var authenticationData = new EntityAuthenticationData
        {
            SaData = JsonSerializer.Serialize(authenticationDetail, DefaultOptions),
            AuthJson = cookieRequest.Json,
            Version = new EntityAuthenticationVersion
            {
                Version = X19.GameVersion
            },
            Aid = otp.Aid.ToString(),
            OtpToken = otp.OtpToken,
            LockTime = 0
        };
        var response = await X19Extensions.Core.HttpWrapper.PostAsync("/authentication-otp", HttpUtil.HttpEncrypt(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(authenticationData, DefaultOptions))));
        var body = await response.Content.ReadAsByteArrayAsync();
        var entity = JsonSerializer.Deserialize<EntityWPFLauncher<EntityAuthenticationOtp>>(HttpUtil.HttpDecrypt(body));
        if (entity == null)
        {
            throw new ErrorCodeException(ErrorCode.LoginError);
        }

        return entity.Code == 0 ? entity.SafeEntity() : throw new EntityX19Exception(entity.Message, entity);
    }

    /// <summary>
    /// 查询服务器详细信息（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetNetGameDetailByIdAsync 移植，自研）。
    /// @param gameId 服务器ID
    /// @return 服务器详细信息
    /// </summary>
    public static async Task<EntityQueryNetGameDetailItem> GetNetGameDetailByIdAsync(string gameId)
    {
        var response = await X19Extensions.Gateway.ApiAsync<EntityWPFLauncher<EntityQueryNetGameDetailItem>>("/item-details/get_v2", new EntityQueryNetGameDetailRequest
        {
            ItemId = gameId
        });
        return response == null ? throw new ErrorCodeException(ErrorCode.DetailError) : response.SafeEntity();
    }

    /// <summary>
    /// 查询服务器地址（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetNetGameServerAddressAsync 移植，自研）。
    /// @param serverId 服务器ID
    /// @return 服务器地址
    /// </summary>
    public static async Task<EntityNetGameServerAddress> GetNetGameServerAddressAsync(string serverId)
    {
        var response = await X19Extensions.Gateway.ApiAsync<EntityWPFLauncher<EntityNetGameServerAddress>>("/item-address/get", new EntityQueryNetGameDetailRequest
        {
            ItemId = serverId
        });
        return response == null ? throw new ErrorCodeException(ErrorCode.AddressError) : response.SafeEntity();
    }

    /// <summary>
    /// 获取服务器上的所有游戏角色（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetNetGameCharactersAsync 移植，自研）。
    /// @param serverId 服务器ID
    /// @return 服务器上的所有游戏角色
    /// </summary>
    public static async Task<EntityGameCharacter[]> GetNetGameCharactersAsync(string gameId)
    {
        var response = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityGameCharacter>>("/game-character/query/user-game-characters", new EntityQueryGameCharacters
        {
            GameId = gameId,
            UserId = InfoManager.GetUserId()
        });
        return response == null ? throw new ErrorCodeException() : response.SafeEntity();
    }

    /// <summary>
    /// 创建游戏角色（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.CreateCharacterAsync 移植，自研）。
    /// @param gameId 服务器ID
    /// @param roleName 角色名称
    /// </summary>
    public static async Task CreateCharacterAsync(string gameId, string roleName)
    {
        var response = await X19Extensions.Gateway.ApiAsync<object>("/game-character", new EntityGameCharacter
        {
            GameId = gameId,
            UserId = InfoManager.GetUserId(),
            Name = roleName
        });
        if (response == null)
        {
            throw new ErrorCodeException();
        }
    }

    /// <summary>
    /// 获取服务器列表（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetAvailableNetGamesAsync 移植，自研）。
    /// @param offset 偏移量
    /// @param length 数量
    /// @return 服务器列表
    /// </summary>
    public static async Task<EntityNetGameItem[]> GetAvailableNetGamesAsync(int offset, int length = 20)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityNetGameItem>>("/item/query/available", new EntityNetGameRequest
        {
            AvailableMcVersions = [],
            ItemType = 1,
            Length = length,
            Offset = offset,
            MasterTypeId = "2", // 2:网络服务器 3:模组 4:资源[光影/材质包] 5:小游戏/生存地图 6:恐怖/解密地图
            SecondaryTypeId = ""
        });
        return entity == null ? throw new ErrorCodeException() : entity.SafeEntity();
    }

    /// <summary>
    /// 获取免费皮肤列表（账号自动登录校验用，由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetFreeSkinListAsync 移植，自研）。
    /// </summary>
    public static async Task<EntityQueryNetSkinItem[]> GetFreeSkinListAsync(int offset = 0, int length = 20)
    {
        var entity = await X19Extensions.Gateway.ApiAsync<EntitiesWPFLauncher<EntityQueryNetSkinItem>>("/item/query/available", new EntityFreeSkinListRequest
        {
            IsHas = true,
            ItemType = 2,
            Length = length,
            MasterTypeId = 10,
            Offset = offset,
            PriceType = 3,
            SecondaryTypeId = 31
        });
        if (entity == null)
        {
            throw new ErrorCodeException();
        }

        return entity.Code != 0 ? throw new EntityX19Exception(entity.Message, entity) : entity.SafeEntity();
    }

    /// <summary>
    /// 查询租赁服地址（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetGameRentalAddressAsync 移植，自研）。
    /// @param serverId 服务器ID
    /// @param pwd 服务器密码
    /// @return 服务器地址
    /// </summary>
    public static async Task<EntityRentalGameServerAddress> GetGameRentalAddressAsync(string serverId, string? pwd = null)
    {
        // 该接口存在问题，20%概率 因为缺少 引号 导致解析失败
        //  "entity_id": 4664453443934401593,
        var entity = await X19Extensions.Client.ApiAsync<EntityWPFLauncher<EntityRentalGameServerAddress>>("/rental-server-world-enter/get", new EntityQueryRentalGameServerAddress
        {
            ServerId = serverId,
            Password = pwd ?? "none"
        });
        return entity == null ? throw new ErrorCodeException() : entity.SafeEntity();
    }

    /// <summary>
    /// 获取租赁服游戏角色列表（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetRentalGameRolesListAsync 移植，自研）。
    /// @param serverId 服务器ID
    /// @return 服务器上的玩家角色列表
    /// </summary>
    public static async Task<EntityRentalGamePlayerList[]> GetRentalGameRolesListAsync(string serverId)
    {
        var entity = await X19Extensions.Client.ApiAsync<EntitiesWPFLauncher<EntityRentalGamePlayerList>>("/rental-server-player/query/search-by-user-server", new EntityQueryRentalGamePlayerList
        {
            ServerId = serverId,
            Offset = 0,
            Length = 10
        });
        return entity == null ? throw new ErrorCodeException() : entity.SafeEntity();
    }

    /// <summary>
    /// 创建租赁服游戏角色（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.CreateCharacterRental 移植，自研）。
    /// @param serverId 服务器ID
    /// @param roleName 角色名称
    /// </summary>
    public static void CreateCharacterRental(string serverId, string roleName)
    {
        CreateCharacterRentalAsync(serverId, roleName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 创建租赁服游戏角色
    /// @param serverId 服务器ID
    /// @param roleName 角色名称
    /// </summary>
    private static async Task CreateCharacterRentalAsync(string serverId, string roleName)
    {
        var response = await X19Extensions.Gateway.ApiAsync<object>("/rental-server-player", new EntityAddRentalGameRole
        {
            ServerId = serverId,
            UserId = InfoManager.GetUserId(),
            Name = roleName,
            CreateTs = 555555,
            IsOnline = false,
            Status = 0
        });
        if (response == null)
        {
            throw new ErrorCodeException();
        }
    }

    /// <summary>
    /// 获取租赁服列表（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetRentalGameListAsync 移植，自研）。
    /// @param offset 偏移量
    /// @return 服务器列表[普通信息]
    /// </summary>
    public static async Task<EntityRentalGame[]> GetRentalGameListAsync(int offset = 0)
    {
        var entity = await X19Extensions.Client.ApiAsync<EntitiesWPFLauncher<EntityRentalGame>>("/rental-server/query/available-public-server", new EntityQueryRentalGame
        {
            Offset = offset
        });
        return entity == null ? throw new ErrorCodeException() : entity.SafeEntity();
    }

    /// <summary>
    /// 获取租赁服详细信息（由 Nirvana.WPFLauncher.Protocol.NPFLauncher.GetRentalGameDetailsAsync 移植，自研）。
    /// @param entityId 服务器ID
    /// @return 服务器详细信息
    /// </summary>
    public static async Task<EntityRentalGameDetails> GetRentalGameDetailsAsync(string entityId)
    {
        var entity = await X19Extensions.Client.ApiAsync<EntityWPFLauncher<EntityRentalGameDetails>>("/rental-server-details/get", new EntityQueryRentalGameDetail
        {
            ServerId = entityId
        });
        return entity == null ? throw new ErrorCodeException() : entity.SafeEntity();
    }
}