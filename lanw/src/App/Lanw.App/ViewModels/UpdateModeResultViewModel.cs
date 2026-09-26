using System.Collections.ObjectModel;
using Lanw.App.Services;

namespace Lanw.App.ViewModels;

/// <summary>更新清单中的文件行（版本页展示用）。</summary>
public sealed class UpdateFileItemViewModel
{
    public UpdateFileItemViewModel(UpdateFileItem item)
    {
        Path = item.Path;
        SizeText = FormatSize(item.Size);
        Sha256Text = string.IsNullOrEmpty(item.Sha256)
            ? "无校验值"
            : item.Sha256![..Math.Min(12, item.Sha256!.Length)] + "…";
        Url = string.IsNullOrEmpty(item.Url) ? "无下载地址" : item.Url!;
        LocalStateText = item.LocalExists switch
        {
            false => "本地缺失",
            true when item.LocalSizeMatches == false => "本地大小不一致",
            true => "本地已存在（大小一致）",
        };
    }

    public string Path { get; }

    public string SizeText { get; }

    public string Sha256Text { get; }

    public string Url { get; }

    public string LocalStateText { get; }

    private static string FormatSize(long? size)
    {
        if (size is null)
        {
            return "未知大小";
        }

        var value = size.Value;
        return value >= 1024L * 1024L
            ? $"{value / 1024.0 / 1024.0:F1} MB"
            : value >= 1024L
                ? $"{value / 1024.0:F1} KB"
                : $"{value} B";
    }
}

/// <summary>单个更新模式的检查结果（可折叠展示其文件清单）。</summary>
public sealed class UpdateModeResultViewModel
{
    public UpdateModeResultViewModel(UpdateModeResult result)
    {
        Name = result.Name;
        Mode = result.Mode;
        SuccessText = result.Success ? "已获取" : "失败";
        Message = result.Message;

        Files = new ObservableCollection<UpdateFileItemViewModel>(
            result.Files.Select(file => new UpdateFileItemViewModel(file)));

        FilesVisibility = Files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public string Name { get; }

    public string Mode { get; }

    public string SuccessText { get; }

    public string Message { get; }

    public ObservableCollection<UpdateFileItemViewModel> Files { get; }

    public Visibility FilesVisibility { get; }
}
