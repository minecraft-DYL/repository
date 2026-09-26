using Lanw.App.Services;
using Lanw.App.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Lanw.App.Views
{
    /// <summary>
    /// 皮肤页（对应原 Vue skin/Skins.vue）：皮肤列表（预览图/名称/简介/开发者/下载量/点赞数）+ 搜索 +
    /// 分批加载更多 + 上传本地皮肤。
    /// 数据来自进程内直调的皮肤协议（SkinService → Lanw.Public.SkinMessage / NPFLauncher），无 HTTP 中转；
    /// 无网络时由 ViewModel 切换到错误态（页内提示 + 重试），不上抛异常、不崩溃。
    /// 上传本地皮肤：PNG 文件选择器（SkinFilePicker，非打包应用需绑定宿主窗口句柄）→ Texture 协议上传。
    /// 主题跟随系统（ThemeService.FollowSystemTheme）。
    /// </summary>
    public sealed partial class SkinsPage : Page
    {
        public SkinsViewModel ViewModel { get; } = new();

        public SkinsPage()
        {
            this.InitializeComponent();
            ThemeService.FollowSystemTheme(this);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 进入页面即拉取列表（fire-and-forget，状态由 VM 维护）
            ViewModel.LoadCommand.Execute(null);
        }

        private void SkinList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not SkinItemViewModel item || string.IsNullOrWhiteSpace(item.EntityId))
            {
                return;
            }

            // 详情页挂在同一导航 Frame 上，可返回列表
            Frame.Navigate(typeof(SkinDetailPage), item.EntityId);
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;
            ViewModel.SearchCommand.Execute(null);
        }

        private async void UploadLocalSkin_Click(object sender, RoutedEventArgs e)
        {
            // 文件选择器需要 XamlRoot 解析宿主窗口句柄（非打包 WinUI 3 应用）
            var picked = await SkinFilePicker.PickPngAsync(this.XamlRoot);
            if (!picked.Success)
            {
                // 用户取消只给普通提示；真实错误（非 PNG / 过大 / 读取失败）按错误样式展示
                ViewModel.ShowPickMessage(picked.Error, isError: !picked.IsCanceled);
                return;
            }

            await ViewModel.UploadLocalSkinAsync(picked.Bytes!, picked.FileName);
        }
    }
}
