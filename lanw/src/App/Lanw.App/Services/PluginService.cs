using Lanw.Core;
using Lanw.Core.Entities;
using Lanw.Development.Manager;
using Lanw.DevPlugin.Entities;
using Lanw.Public.Entities.Nirvana;
using Lanw.Public.Entities.Plugin;
using Lanw.Public.Message;

namespace Lanw.App.Services;

/// <summary>
/// 插件服务：进程内直调 t20/t21 移植的插件后端（Lanw.Development.Manager.PluginManager、
/// Lanw.Public.Message.PluginMessage / PlugInstoreMessage），不经任何 HTTP 中转。
/// 端点语义对应原服务端 Fantnel/Servlet/PluginsController：
///   PluginsListController  /api/plugins/get | toggle | delete | dependence
///   PluginsShopController  /api/pluginstore/get | detail | install
/// 所有调用都投递到线程池：PluginManager 内部有同步文件 IO 与程序集加载（Assembly.LoadFile），
/// PlugInstoreMessage 走同步 HTTP（GetAwaiter().GetResult()），直接在 UI 线程调用会冻结界面。
/// </summary>
public sealed class PluginService
{
    /// <summary>已安装插件列表（/api/plugins/get → PluginManager.GetPluginStates）。</summary>
    public Task<EntityPluginState[]> GetInstalledPluginsAsync()
        => Task.Run(PluginManager.GetPluginStates);

    /// <summary>切换插件启用状态（/api/plugins/toggle，auto = -1 表示按当前状态自动切换）。</summary>
    public Task TogglePluginAsync(string id)
        => Task.Run(() => PluginManager.TogglePlugin(id));

    /// <summary>
    /// 删除插件（/api/plugins/delete → PluginManager.DeletePlugin）。
    /// 原控制器在删除后 Thread.Sleep(1000)「避免执行过快」，此处保留同样的延迟语义，
    /// 但延迟发生在后台线程，不阻塞界面。
    /// </summary>
    public Task DeletePluginAsync(string id)
        => Task.Run(() =>
        {
            PluginManager.DeletePlugin(id);
            Thread.Sleep(1000);
        });

    /// <summary>插件商城列表（/api/pluginstore/get → PlugInstoreMessage.GetPluginList，带缓存分页）。</summary>
    public Task<EntityComponents[]> GetStoreListAsync(int offset = 0, int limit = 10)
        => Task.Run(() => PlugInstoreMessage.GetPluginList(offset, limit));

    /// <summary>插件商城详情（/api/pluginstore/detail → PlugInstoreMessage.GetPluginDetail）。</summary>
    public Task<EntityResponse<EntityPlugin>?> GetPluginDetailAsync(string id)
        => Task.Run(() => PlugInstoreMessage.GetPluginDetail(id));

    /// <summary>安装插件及其依赖（/api/pluginstore/install → PlugInstoreMessage.Install）。</summary>
    public Task InstallPluginAsync(string id)
        => Task.Run(() => PlugInstoreMessage.Install(id));

    /// <summary>服务器插件依赖列表（/api/plugins/dependence → PluginMessage.GetDependenceList）。</summary>
    public Task<List<EntityDependence>> GetDependenceListAsync(string? id, string? version)
        => Task.Run(() => PluginMessage.GetDependenceList(id, version));

    /// <summary>自动更新插件开关（LanwConfig: autoUpdatePlugin，与设置页同一键）。</summary>
    public bool AutoUpdatePlugin
    {
        get => LanwConfig.GetValue<bool>("autoUpdatePlugin");
        set => LanwConfig.SetValue("autoUpdatePlugin", value);
    }
}
