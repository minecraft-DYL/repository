using System.Text.Json;
using Lanw.App.Models;
using Lanw.App.ViewModels;
using Lanw.Public.Entities.Nirvana;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Streams;

namespace Lanw.App.Views
{
    /// <summary>
    /// 账号管理页：唯一的手动登录入口 + 账号管理。
    /// 合并了原「登录」页的职责（用户要求删掉冗余的登录页）：点击「添加账号」弹出凭据输入框，
    /// 登录成功后协议会把账号写入 account.json，本页随后刷新列表。
    /// 另含「随机登录」（打开账号池网页端，自行获取随机 4399 账号）。
    /// 4399 / 4399com 验证码自动识别失败时，弹出图片验证码对话框让用户手动输入并重试。
    /// 数据来自进程内持久化存储（AccountRepository / account.json）；主题跟随系统（根 Frame ElementTheme.Default）。
    /// </summary>
    public sealed partial class AccountsPage : Page
    {
        public AccountsViewModel ViewModel { get; } = new();

        public AccountsPage()
        {
            this.InitializeComponent();
            // 视图负责弹窗，VM 通过回调请求：4399 图片验证码 / Geetest 网页验证。
            ViewModel.CaptchaPrompt = ShowCaptchaDialogAsync;
            ViewModel.GeetestPrompt = ShowGeetestDialogAsync;
            ViewModel.EditPrompt = ShowEditDialogAsync;
        }

        /// <summary>
        /// 「添加账号」：弹窗填写登录类型 / 账号 / 密码，登录并保存。
        /// 登录失败时不关闭弹窗（args.Cancel），把错误显示在弹窗内，用户改完可重试。
        /// </summary>
        private async void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            var typeBox = new ComboBox
            {
                Header = "登录类型",
                DisplayMemberPath = "DisplayName",
                ItemsSource = ViewModel.LoginTypes,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "选择登录类型",
            };

            // 默认 4399com（原版 GameAccounts.vue 新增账号弹窗的默认类型），取不到则第一项。
            var types = ViewModel.LoginTypes.ToList();
            var defaultIndex = types.FindIndex(t => t.Key == "4399com");
            typeBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;

            var nameBox = new TextBox { Header = "名称", PlaceholderText = "账号备注名（列表展示用，必填）" };
            var accountBox = new TextBox { Header = "账号", PlaceholderText = "请输入账号（Cookie 型可留空）" };
            var passwordBox = new PasswordBox { Header = "密码 / Cookie", PlaceholderText = "请输入密码或 Cookie 凭据" };
            var hint = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Text = "提示：4399 / 4399com 会先自动识别验证码，失败时会弹窗让你手动输入。登录成功后该账号会自动保存到列表。",
            };
            var statusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };

            var panel = new StackPanel { Spacing = 10, MinWidth = 380 };
            panel.Children.Add(nameBox);
            panel.Children.Add(typeBox);
            panel.Children.Add(accountBox);
            panel.Children.Add(passwordBox);
            panel.Children.Add(hint);
            panel.Children.Add(statusText);

            var dialog = new ContentDialog
            {
                Title = "添加账号",
                Content = panel,
                PrimaryButtonText = "登录并保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    var typeKey = (typeBox.SelectedItem as LoginTypeOption)?.Key;

                    // 表单校验：对齐原版 addNewAccount 的文案与规则（名称/密码必填；非 cookie 型账号必填）
                    var formError = AccountsViewModel.ValidateAccountForm(nameBox.Text, accountBox.Text, passwordBox.Password, typeKey);
                    if (formError is not null)
                    {
                        statusText.Text = formError;
                        args.Cancel = true;
                        return;
                    }

                    statusText.Text = "登录中…";
                    var ok = await ViewModel.LoginWithCredentialsAsync(typeKey, accountBox.Text ?? string.Empty, passwordBox.Password ?? string.Empty, nameBox.Text);
                    statusText.Text = ViewModel.StatusMessage;
                    if (ok)
                    {
                        passwordBox.Password = string.Empty;
                    }
                    else
                    {
                        args.Cancel = true; // 失败保留弹窗，便于修改后重试
                    }
                }
                finally
                {
                    deferral.Complete();
                }
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// 「编辑账号」：弹窗预填 名称 / 类型 / 账号 / 密码，保存即写回持久化
        /// （对应原版 GameAccounts.vue 的 editAccount → 编辑弹窗 → updateAccount）。
        /// 校验失败不关窗，错误显示在弹窗内；cookie 型隐藏「账号」并把密码位改为 Cookie/Auth（原版条件表单）。
        /// </summary>
        private async Task ShowEditDialogAsync(AccountItemViewModel item)
        {
            var types = ViewModel.LoginTypes.ToList();

            var nameBox = new TextBox { Header = "名称", Text = item.Account.Name ?? string.Empty };
            var typeBox = new ComboBox
            {
                Header = "账号类型",
                DisplayMemberPath = "DisplayName",
                ItemsSource = types,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var typeIndex = types.FindIndex(t => t.Key == (item.Account.Type ?? "4399"));
            typeBox.SelectedIndex = typeIndex >= 0 ? typeIndex : 0;

            var accountBox = new TextBox { Header = "账号", Text = item.Account.Account ?? string.Empty };
            var passwordBox = new TextBox { Header = "密码", Text = item.Account.Password ?? string.Empty };
            var statusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };

            // 原版的按类型条件表单：cookie 型没有「账号」，密码位填 Cookie/Auth。
            void ApplyTypeLayout()
            {
                var isCookie = (typeBox.SelectedItem as LoginTypeOption)?.Key == "cookie";
                accountBox.Visibility = isCookie ? Visibility.Collapsed : Visibility.Visible;
                passwordBox.Header = isCookie ? "Cookie/Auth" : "密码";
                passwordBox.AcceptsReturn = isCookie;
            }

            typeBox.SelectionChanged += (_, _) => ApplyTypeLayout();
            ApplyTypeLayout();

            var panel = new StackPanel { Spacing = 10, MinWidth = 380 };
            panel.Children.Add(nameBox);
            panel.Children.Add(typeBox);
            panel.Children.Add(accountBox);
            panel.Children.Add(passwordBox);
            panel.Children.Add(statusText);

            var dialog = new ContentDialog
            {
                Title = "编辑账号",
                Content = panel,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            dialog.PrimaryButtonClick += (_, args) =>
            {
                var typeKey = (typeBox.SelectedItem as LoginTypeOption)?.Key;
                if (!ViewModel.ApplyAccountEdit(item, nameBox.Text ?? string.Empty, accountBox.Text ?? string.Empty, passwordBox.Text ?? string.Empty, typeKey, out var error))
                {
                    statusText.Text = error;
                    args.Cancel = true; // 失败保留弹窗，便于修改后重试
                }
            };

            await dialog.ShowAsync();
        }

        /// <summary>「随机登录」：交由 VM 编排（打开账号池网页端）。</summary>
        private void RandomLogin_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.RandomLoginCommand.CanExecute(null))
            {
                ViewModel.RandomLoginCommand.Execute(null);
            }
        }

        /// <summary>
        /// 4399 图片验证码弹窗（对应原版「验证码登录」模态框）：验证码图片（可点击刷新）+ 输入框 + 确认/取消。
        /// 返回用户输入的验证码；取消时返回 null。
        /// </summary>
        private async Task<string?> ShowCaptchaDialogAsync()
        {
            var image = new Image
            {
                Height = 72,
                HorizontalAlignment = HorizontalAlignment.Left,
                Stretch = Stretch.Uniform,
            };
            image.Source = await ToBitmapAsync(ViewModel.CaptchaImage);
            image.Tapped += async (_, _) => await RefreshCaptchaAsync(image);

            var input = new TextBox
            {
                Header = "验证码",
                PlaceholderText = "请输入图片中的验证码",
            };

            var refresh = new Button { Content = "刷新验证码" };
            refresh.Click += async (_, _) => await RefreshCaptchaAsync(image, input);

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = "4399 账号登录需要输入验证码（点击图片或按钮可刷新）。",
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(image);
            panel.Children.Add(refresh);
            panel.Children.Add(input);

            var dialog = new ContentDialog
            {
                Title = "验证码登录",
                Content = panel,
                PrimaryButtonText = "确认登录",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? input.Text?.Trim() : null;
        }

        private async Task RefreshCaptchaAsync(Image image, TextBox? input = null)
        {
            image.Source = await ToBitmapAsync(await ViewModel.LoadCaptchaAsync());
            if (input is not null)
            {
                input.Text = string.Empty;
                input.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Geetest 人机验证弹窗（对应原版 GameAccounts.vue 的 Geetest 组件，captchaId
        /// fefebb64747ce99237ecdf1830f0ae63 / product bind）：用 WebView2 承载本地 Assets/geetest.html，
        /// 验证成功后网页经 window.chrome.webview.postMessage 回传 validate，此处组装 EntityGeeTest 交回 VM。
        /// 取消或关闭返回 null。
        /// </summary>
        private async Task<EntityGeeTest?> ShowGeetestDialogAsync()
        {
            var tcs = new TaskCompletionSource<EntityGeeTest?>();

            var webView = new WebView2 { Width = 480, Height = 420 };
            var statusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Text = "正在加载账号池页面…" };

            var panel = new StackPanel { Spacing = 8, MinWidth = 480 };
            panel.Children.Add(webView);
            panel.Children.Add(statusText);

            var dialog = new ContentDialog
            {
                Title = "随机登录 · 账号池",
                Content = panel,
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot,
            };

            try
            {
                await webView.EnsureCoreWebView2Async();

                // 把 Assets 目录映射成 https 虚拟主机，让页面有正常来源（极验脚本与 postMessage 都需要）。
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "npyyds.top",
                    Path.Combine(AppContext.BaseDirectory, "Assets"),
                    CoreWebView2HostResourceAccessKind.Allow);

                webView.CoreWebView2.WebMessageReceived += (_, args) =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(args.WebMessageAsJson);
                        var root = doc.RootElement;
                        var type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                        switch (type)
                        {
                            case "success":
                            {
                                var v = root.GetProperty("validate");
                                var entity = new EntityGeeTest
                                {
                                    LotNumber = GetString(v, "lot_number"),
                                    PassToken = GetString(v, "pass_token"),
                                    GenTime = GetString(v, "gen_time"),
                                    CaptchaOutput = GetString(v, "captcha_output"),
                                };
                                tcs.TrySetResult(entity);
                                dialog.Hide();
                                break;
                            }

                            case "close":
                                tcs.TrySetResult(null);
                                dialog.Hide();
                                break;

                            case "status":
                            case "error":
                            case "loading":
                                if (root.TryGetProperty("message", out var messageEl))
                                {
                                    statusText.Text = messageEl.GetString() ?? string.Empty;
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        statusText.Text = "账号池页面消息解析失败：" + ex.Message;
                    }
                };

                // 需求变更：不再自建极验托管页，直接打开账号池官方网页端（它自带验证码）
                webView.CoreWebView2.Navigate("http://110.42.70.32:23148/nac4399/");
            }
            catch (Exception ex)
            {
                // WebView2 运行时缺失 / 初始化失败也要给明确提示，绝不静默。
                statusText.Text = "无法初始化网页内核（WebView2）：" + ex.Message;
            }

            var result = await dialog.ShowAsync();
            if (tcs.Task.IsCompleted)
            {
                return await tcs.Task;
            }

            // 用户在验证完成前点了取消。
            tcs.TrySetResult(null);
            return result == ContentDialogResult.Primary ? await tcs.Task : null;
        }

        private static string GetString(JsonElement element, string name)
            => element.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

        /// <summary>把验证码图片字节转成可显示位图。</summary>
        private static async Task<BitmapImage?> ToBitmapAsync(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
    }
}
