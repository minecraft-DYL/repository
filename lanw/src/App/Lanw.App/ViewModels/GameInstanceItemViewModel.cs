using CommunityToolkit.Mvvm.ComponentModel;

namespace Lanw.App.ViewModels;

/// <summary>
/// 运行中游戏实例列表项（对应原 Vue GameLaunchManager.vue 表格的
/// 角色名称 / 游戏名称 / 用户ID / 游戏版本 / 操作）。
/// </summary>
public sealed partial class GameInstanceItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Id { get; set; }

    [ObservableProperty]
    public partial string RoleName { get; set; }

    [ObservableProperty]
    public partial string GameName { get; set; }

    [ObservableProperty]
    public partial string UserId { get; set; }

    [ObservableProperty]
    public partial string GameVersion { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    public GameInstanceItemViewModel(
        string id,
        string roleName,
        string gameName,
        string userId,
        string gameVersion,
        string statusText)
    {
        Id = id;
        RoleName = roleName;
        GameName = gameName;
        UserId = userId;
        GameVersion = gameVersion;
        StatusText = statusText;
    }

    /// <summary>关联的运行中实例 ID（关闭时使用）。</summary>
    public int InstanceId { get; init; }
}
