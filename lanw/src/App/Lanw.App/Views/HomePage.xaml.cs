using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Lanw.App.Views;

/// <summary>
/// 主页（对应原 Vue Home.vue + /api/home 数据）：公告卡片、版本信息、随机名生成器、主题包导入。
/// 数据经 HomeMessage（内嵌服务器等价）拉取，主题跟随系统（ThemeResource）。
/// </summary>
public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; } = new();

    public HomePage()
    {
        this.InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // 进入主页即拉取公告/版本信息（fire-and-forget，状态由 VM 维护）
        ViewModel.LoadCommand.Execute(null);
    }

    /// <summary>选择主题文件（对应原版 handleFileSelect → input accept=".fant.json"）。</summary>
    private async void PickThemeFile_Click(object sender, RoutedEventArgs e)
    {
        var pick = await ThemeFilePicker.PickThemeFileAsync(this.XamlRoot);
        await HandleThemePickAsync(pick);
    }

    /// <summary>拖拽经过时允许复制（对应原版 @dragover.prevent）。</summary>
    private void ThemeDrop_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        if (e.DragUIOverride is not null)
        {
            e.DragUIOverride.Caption = "拖入以应用主题";
        }
    }

    /// <summary>拖拽放下（对应原版 @drop.prevent="handleFileDrop" → handleFile）。</summary>
    private async void ThemeDrop_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                ViewModel.StatusMessage = "拖入的内容不是文件，请拖入 .fant.json 主题文件。";
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();
            var item = items.Count > 0 ? items[0] : null;
            await HandleThemePickAsync(await ThemeFilePicker.ReadThemeFileAsync(item));
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = "导入主题失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 处理选中的主题包：对应原版弹出「正在应用主题，请稍后…」→ 应用主题 → 展示结果 →
    /// 确定后 reload() 重新拉取主页数据。选择失败 / 解析失败也走同一提示框，绝不静默。
    /// </summary>
    private async Task HandleThemePickAsync(ThemePickResult pick)
    {
        if (pick.IsCanceled)
        {
            ViewModel.StatusMessage = "已取消选择文件";
            return;
        }

        if (!pick.Success)
        {
            await ShowThemeMessageAsync(pick.Error);
            return;
        }

        var message = new TextBlock
        {
            Text = "正在应用主题，请稍后…",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        };

        var dialog = new ContentDialog
        {
            Title = "提示",
            Content = message,
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };

        var shown = dialog.ShowAsync().AsTask();
        var (ok, text) = await ViewModel.ApplyThemeAsync(pick.Json);
        message.Text = text;
        await shown;

        if (ok)
        {
            // 对应原版 Alert 的 @ok="reload()"
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private async Task ShowThemeMessageAsync(string text)
    {
        await new ContentDialog
        {
            Title = "提示",
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 },
            CloseButtonText = "确定",
            XamlRoot = this.XamlRoot,
        }.ShowAsync();
    }
}
