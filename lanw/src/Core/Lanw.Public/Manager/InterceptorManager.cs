using Lanw.Cipher.Cipher.Nirvana.Connection;
using Lanw.Core.Entities.Login;
using Lanw.Core.Manager;
using Lanw.Development;
using Lanw.DevPlugin.Entities;
using Lanw.Heypixel;
using Lanw.Public.Message;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameCharacters;
using Lanw.WPFLauncher.Entities.WPFLauncher.NetGame.GameDetails;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;
using Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame.GameCharacters;
using Serilog;

namespace Lanw.Public.Manager;

/// <summary>
/// 代理模式拦截器编排（由 Nirvana.Public.Manager.InterceptorManager 移植）。
/// </summary>
public class InterceptorManager {
    private readonly EntityAccount _availableUser;

    private readonly string _entityId;
    private readonly string _mods;
    private readonly string _versionName;
    public readonly Interceptor Interceptor;

    static InterceptorManager()
    {
        HeypixelProtocol.Init();
    }

    public InterceptorManager(EntityQueryNetGameDetailItem server, EntityGameCharacter character, EntityMcVersion version, EntityNetGameServerAddress address, string mods, int port)
    {
        _mods = mods;
        _versionName = version.Name;
        _entityId = server.EntityId;
        _availableUser = InfoManager.GetGameAccount();
        // 创建代理
        Interceptor = Interceptor.CreateInterceptor(false, mods, server.EntityId, server.Name, version.Name, address.Host, address.Port, character.Name, _availableUser, YggdrasilCallback, port);
    }

    public InterceptorManager(EntityRentalGameDetails server, EntityRentalGamePlayerList character, string versionName, EntityRentalGameServerAddress address, string mods, int port)
    {
        _mods = mods;
        _versionName = versionName;
        _entityId = server.EntityId;
        _availableUser = InfoManager.GetGameAccount();
        // 创建代理
        Interceptor = Interceptor.CreateInterceptor(true, mods, server.EntityId, server.ServerName, versionName, address.McServerHost, address.McServerPort, character.Name, _availableUser, YggdrasilCallback, port);
    }

    private void YggdrasilCallback(InterceptorConfig config, string serverId)
    {
        NetEaseConnection.CreateAuthenticator(serverId, config.GameId, _versionName, _mods, _availableUser, success => {
            if (!success) {
                try {
                    AccountMessage.AutoUpdateAccount(_availableUser, () => { ActiveGameAndProxies.CloseProxy(Interceptor); });
                } catch (Exception e) {
                    Log.Error("认证失败: {0}: {1}", _availableUser.Account, e.Message);
                }
            }
        });
    }
}
