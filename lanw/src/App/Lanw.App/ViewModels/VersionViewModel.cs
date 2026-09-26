using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lanw.App.Services;
using Lanw.Core;

namespace Lanw.App.ViewModels;

/// <summary>
/// 版本页视图模型（对应原版 /api/version 与 others/Version.vue 的提示）。
/// 展示当前版本号（LanwProgram.Version/VersionId/Mode/Arch），并提供「检查更新」：
/// 进程内直调 t17 移植的更新检查（只读清单，经 VersionService），无 HTTP 服务端。
/// </summary>
public sealed partial class VersionViewModel : ObservableObject
{
    /// <summary>当前版本号（LanwProgram.Version）。</summary>
    [ObservableProperty]
    public partial string Version { get; set; }

    /// <summary>版本序号（LanwProgram.VersionId）。</summary>
    [ObservableProperty]
    public partial string VersionId { get; set; }

    /// <summary>运行模式（win / linux / mac）。</summary>
    [ObservableProperty]
    public partial string Mode { get; set; }

    /// <summary>系统架构（x64 / arm64）。</summary>
    [ObservableProperty]
    public partial string Arch { get; set; }

    /// <summary>更新器版本（LanwProgram.UpdateVersion，与后端更新接口比对用）。</summary>
    [ObservableProperty]
    public partial string UpdateVersion { get; set; }

    /// <summary>是否发布版（LanwProgram.Release）。</summary>
    [ObservableProperty]
    public partial string ReleaseText { get; set; }

    /// <summary>是否最新版（LanwProgram.LatestVersion）。</summary>
    [ObservableProperty]
    public partial string LatestText { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdateCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial Visibility ResultsVisibility { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyVisibility { get; set; }

    /// <summary>各更新模式的检查结果。</summary>
    public ObservableCollection<UpdateModeResultViewModel> Results { get; } = [];

    public VersionViewModel()
    {
        Version = LanwProgram.Version;
        VersionId = LanwProgram.VersionId.ToString();
        Mode = LanwProgram.Mode;
        Arch = LanwProgram.Arch;
        UpdateVersion = LanwProgram.UpdateVersion;
        ReleaseText = LanwProgram.Release ? "发布版" : "调试版（跳过版本检测）";
        LatestText = LanwProgram.LatestVersion ? "最新版本" : "非最新版本，建议更新";
        StatusMessage = "点击「检查更新」获取远端更新清单";
        IsBusy = false;
        ResultsVisibility = Visibility.Collapsed;
        EmptyVisibility = Visibility.Visible;
    }

    /// <summary>检查更新（只读：不会下载文件、不会退出进程）。</summary>
    [RelayCommand(CanExecute = nameof(CanCheckUpdate))]
    private async Task CheckUpdateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在获取更新清单…";

        try
        {
            var results = await VersionService.CheckAllAsync().ConfigureAwait(true);

            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(new UpdateModeResultViewModel(result));
            }

            var ok = results.Count(result => result.Success);
            StatusMessage = $"检查完成：{ok}/{results.Count} 项获取成功（只读检查，不会下载文件）";
        }
        catch (Exception e)
        {
            StatusMessage = "检查更新失败：" + e.Message;
        }
        finally
        {
            IsBusy = false;
            RefreshVisibility();
        }
    }

    private bool CanCheckUpdate() => !IsBusy;

    private void RefreshVisibility()
    {
        var hasResults = Results.Count > 0;
        ResultsVisibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = hasResults ? Visibility.Collapsed : Visibility.Visible;
    }
}
