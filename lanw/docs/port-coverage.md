# Fantnel → lanw 功能覆盖审计报告

> 审计对象：参考源 `D:\ku\traecode\_fantnel_ref`（GitHub `NirvanaTec/fantnel` @ `70134bf8`）
> 移植目标：`D:\ku\traecode\lanw`（.NET 10 + WinUI 3 桌面端，无 HTTP 层）
> 审计方式：只读。全部结论均由 `glob` / `grep` / `read` 逐条核对文件路径与方法名，未证实的一律标注「未确认」。
> 审计范围不含 `obj/`、`bin/` 生成物，未执行 `dotnet build`。
>
> ⚠️ **并发变更提示**：审计期间工作区存在并行开发（源文件 `LastWriteTime` 显示 2 小时内有他人改动）。本报告对**主题包导入**相关条目已按最新状态复核并更新（见 §2.1、§3.7、§4-P2#15）。若报告与工作区再次不一致，以工作区为准。

---

## 0. 一句话结论

**lanw 已把 Fantnel 的「数据面」基本搬完（列表/详情/插件/皮肤/内存日志/主题包导入），但「动作面」缺口集中在三条主线：① 从服务器/租赁服详情页一键「登录+选角色+启动游戏/启动代理」；② 账号的编辑与删除确认；③ Java 运行时自动安装 与 启动期初始化（插件管理器、缓存预热、在线心跳）。按功能点计覆盖率 64.7%（加权后 74.1%）。**

### 统计（以「功能点」为最小单位）

| 维度 | 已移植 | 部分移植 | 未移植 | 小计 |
|---|---|---|---|---|
| 前端页面功能点 | 94 | 25 | 23 | **142** |
| 后端接口 / 业务动作 | 36 | 13 | 10 | **59** |
| **合计** | **130** | **38** | **33** | **201** |

- **覆盖率（严格）**：130 / 201 = **64.7%**
- **覆盖率（部分移植按 0.5 计）**：(130 + 19) / 201 = **74.1%**
- 其中 5 项属于「桌面端设计性废弃」（源端窗口最小化/关闭/拖拽、`/api/exit` 等），若剔除，有效总数 196，严格覆盖率 **66.3%**，加权 **76.0%**。
- 另有 **lanw 超出源项目的净新增** 7 大块（见 §6），不计入覆盖率分子/分母。
- 审计期间并行落地的已完成项：**主题包导入**（`.fant.json` 拖拽/选择/应用，含 `SafeTheme` 校验与更新触发）——已从「未移植」更正为「已移植」。

---

## 1. 前端页面覆盖表（19 条路由）

| # | 源路由 | 源组件 | lanw 页面 | 状态 | 证据 |
|---|---|---|---|---|---|
| 1 | `/login` | `views/nirvana/NirvanaLogin.vue` | `UserHomePage`（未登录卡内嵌登录表单） | 部分移植 | `src/App/Lanw.App/Views/UserHomePage.xaml:106-151`；`ViewModels/UserHomeViewModel.cs:142`。**缺**独立登录页与「已登录自动跳转」路由语义 |
| 2 | `/login-socket` | `views/nirvana/NirvanaLoginSocket.vue` | `UserHomePage`（合并） | 部分移植 | 同上；`days>0` 校验在 `UserHomeViewModel.cs:165` |
| 3 | `/` | `views/Home.vue` | `Views/HomePage.xaml` + `ViewModels/HomeViewModel.cs` | 已移植 | `HomeViewModel.cs:70 LoadAsync`、`:149 ApplyThemeAsync`；`Views/HomePage.xaml.cs:29-112` 主题导入 |
| 4 | `/game-accounts` | `views/game/GameAccounts.vue` | `Views/AccountsPage.xaml` + `ViewModels/AccountsViewModel.cs` | 部分移植 | `AccountsViewModel.cs:100 LoginWithCredentialsAsync`、`:202 RandomLoginAsync`；**缺**编辑账号 |
| 5 | `/servers` | `views/game/netgame/Servers.vue` | `Views/ServersPage.xaml` + `ViewModels/ServersViewModel.cs` | 已移植 | `ServersViewModel.cs:80 LoadAsync`、`:127 LoadMoreAsync` |
| 6 | `/server/:id` | `views/game/netgame/ServerDetail.vue` | `Views/ServerDetailPage.xaml` + `ViewModels/ServerDetailViewModel.cs` | 部分移植 | `ServerDetailViewModel.cs:103 LoadCommand`、`:147 ShowImage`；**缺** Launch 弹窗整套 |
| 7 | `/skins` | `views/game/skin/Skins.vue` | `Views/SkinsPage.xaml` + `ViewModels/SkinsViewModel.cs` | 已移植 | `SkinsViewModel.cs:143 Load`、`:193 Search`、`:234 LoadMore` |
| 8 | `/skin/:id` | `views/game/skin/SkinDetail.vue` | `Views/SkinDetailPage.xaml` + `ViewModels/SkinDetailViewModel.cs` | 部分移植 | `SkinDetailViewModel.cs:180 ApplyAsync`；**缺**账号下拉选择 |
| 9 | `/plugins` | `views/plugin/Plugins.vue` | `Views/PluginsPage.xaml` + `ViewModels/PluginsViewModel.cs` | 已移植 | `PluginsPage.xaml.cs:56 DeletePlugin_Click`（含确认框）、`:37 PluginToggle_Toggled` |
| 10 | `/plugin-store` | `views/plugin/PluginStore.vue` | `Views/PluginStorePage.xaml` + `ViewModels/PluginStoreViewModel.cs` | 已移植 | `PluginStorePage.xaml:33` AutoSuggestBox、`:150 LoadMoreCommand` |
| 11 | `/plugin/:id` | `views/plugin/PluginDetail.vue` | `Views/PluginDetailPage.xaml` + `ViewModels/PluginDetailViewModel.cs` | 部分移植 | `PluginDetailViewModel.cs:136/187` 两个 RelayCommand；**缺**安装确认弹窗的依赖清单文案 |
| 12 | `/proxy-manager` | `views/game/ProxyManager.vue` | `Views/ProxyManagerPage.xaml` + `ViewModels/ProxyManagerViewModel.cs` | 部分移植 | `ProxyManagerViewModel.cs:194 CloseAllProxies`、`:261 CopyConfigPath`；**缺**关闭确认框 |
| 13 | `/game-manager` | `views/game/GameLaunchManager.vue` | `Views/GameLaunchManagerPage.xaml` + `ViewModels/GameLaunchManagerViewModel.cs` | 部分移植 | `GameLaunchManagerViewModel.cs:295 LaunchCommand`；**缺**关闭确认框 |
| 14 | `/game-rental/` | `views/game/rental/GameRental.vue` | `Views/GameRentalPage.xaml` + `ViewModels/GameRentalViewModel.cs` | 已移植 | `GameRentalViewModel.cs:80 LoadAsync`；排序在 `Services/RentalGameService.cs:41` |
| 15 | `/game-rental/:id` | `views/game/rental/GameRentalDetail.vue` | `Views/GameRentalDetailPage.xaml` + `ViewModels/GameRentalDetailViewModel.cs` | 部分移植 | `GameRentalDetailViewModel.cs:138 LoadAsync`、`:183 ReloadPlayers`、`:195 CopyAddress`；**缺** Launch 弹窗 |
| 16 | `/user` | `views/nirvana/UserHome.vue` | `Views/UserHomePage.xaml` + `ViewModels/UserHomeViewModel.cs` | 部分移植 | `UserHomeViewModel.cs:73 LoadAsync`、`:192 Logout`；**缺**失败倒计时跳转 |
| 17 | `/settings` | `views/Settings.vue` | `Views/SettingsPage.xaml` + `ViewModels/SettingsViewModel.cs` | 已移植 | `SettingsViewModel.cs:77 Load`、`:85 Save`、`:115 ResetToDefault`（10 项设置全覆盖） |
| 18 | `/logs` | `views/others/Logs.vue` | `Views/LogsPage.xaml` + `ViewModels/LogsViewModel.cs` | 已移植 | `LogsViewModel.cs:68 Refresh`、`:76 Clear`；数据源 `Services/ViewLogService.cs:48` |
| 19 | `/version` | `views/others/Version.vue` | `Views/VersionPage.xaml` + `ViewModels/VersionViewModel.cs` | 已移植 | `VersionViewModel.cs:60`、`:77 CheckUpdateAsync`（源页仅一句提示，lanw 为增强） |
| — | `App.vue`（外壳） | `web/src/App.vue` | `Views/ShellPage.xaml` | 部分移植 | `ShellPage.xaml:17-132` 12 个导航项；主题三态切换被替换为跟随系统 `Services/ThemeService.cs:15` |

**页面级结论**：19 条路由全部有落点，**0 条完全缺失**；其中 10 条「已移植」、9 条「部分移植」。

---

## 2. 功能点覆盖表

> 状态说明：`已移植` = lanw 有可核对的方法/控件；`部分` = 只做了一部分；`未` = 找不到；`N/A` = 桌面端设计性废弃。
> 源位置为「组件:行」，lanw 位置为「仓库相对路径:行」。

### 2.1 Home.vue / 外壳（App.vue）

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 公告卡 ad1/ad2/ad3（名称+文字） | Home.vue:12-25 | `src/App/Lanw.App/ViewModels/HomeViewModel.cs:86-102` | 已移植 |
| 主题文件拖拽区（`.fant.json`） | Home.vue:28-40 | `Views/HomePage.xaml:98-106`（`AllowDrop`/`DragOver`/`Drop`）；`Views/HomePage.xaml.cs:36-68` | 已移植 |
| 「选择 主题 文件」按钮 + 文件选择 | Home.vue:35-38 | `Views/HomePage.xaml:120` + `HomePage.xaml.cs:29-34`；`Services/ThemeFilePicker.cs:42` | 已移植 |
| 主题下载外链（涅槃科技） | Home.vue:32-34 | `Views/HomePage.xaml:123`（「前往涅槃科技下载主题」） | 已移植 |
| 应用主题弹窗 + `setThemeSwitch` | Home.vue:42,79-102 | `HomeViewModel.cs:149 ApplyThemeAsync` → `ThemeMessage.SwitchThemeAsync`；`HomePage.xaml.cs:101,112` | 已移植 |
| 侧边导航框架 + 导航项 | App.vue:134-360 | `src/App/Lanw.App/Views/ShellPage.xaml:12-135` | 已移植 |
| 三态主题切换（light/dark/gray） | App.vue:36-43 | `src/App/Lanw.App/Services/ThemeService.cs:15`（仅跟随系统） | 部分 |
| 顶部涅槃账号显示 | App.vue:26-27 | `src/App/Lanw.App/ViewModels/UserHomeViewModel.cs:225` | 已移植 |
| 窗口最小化/关闭按钮 | App.vue:138-143 | — | N/A |
| 窗口拖拽区（`window:drag-*`） | App.vue:103-130 | — | N/A |
| `code==5` 消息 → `initWindow` 登录守卫 | App.vue:65-101 | — | N/A |
| 启动登录守卫（days<1 → 登录页） | App.vue:88-96 | `UserHomeViewModel.cs:73 LoadAsync`（无跳转，为占位卡） | 部分 |

### 2.2 登录（NirvanaLogin / NirvanaLoginSocket）

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 账号输入框 | NirvanaLogin.vue:13-18 | `Views/UserHomePage.xaml:123-128` | 已移植 |
| 密码输入框 | NirvanaLogin.vue:20-25 | `Views/UserHomePage.xaml:129-134` | 已移植 |
| 「登录」按钮 | NirvanaLogin.vue:27-31 | `Views/UserHomePage.xaml:135-138` | 已移植 |
| 登录成功跳转 `/user` | NirvanaLogin.vue:57-60 | `ViewModels/UserHomeViewModel.cs:165-170` | 已移植 |
| 已登录自动跳转 | NirvanaLogin.vue:69-77 | `UserHomeViewModel.cs:73` | 部分 |
| `days>0` 校验（无天数拒绝登录） | NirvanaLoginSocket.vue:54-60 | `UserHomeViewModel.cs:165,173` | 已移植 |
| 「注册账号」外链 | NirvanaLogin.vue:36 | `Views/UserHomePage.xaml:141-144` | 已移植 |
| 「涅槃商城」外链 | NirvanaLogin.vue:37 | `Views/UserHomePage.xaml:145-148` | 已移植 |
| 标题/版权页脚（© 涅槃科技） | NirvanaLogin.vue:5-8,34 | `Views/UserHomePage.xaml:29-36` | 已移植 |

### 2.3 账号管理（GameAccounts.vue）

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 账号表格（名称/账号/类型/UserId） | GameAccounts.vue:6-31 | `Views/AccountsPage.xaml:79-144` | 已移植 |
| 行内「登录」 | GameAccounts.vue:24 | `Views/AccountsPage.xaml:124-129`（切换/设为当前） | 已移植 |
| 行内「编辑」 | GameAccounts.vue:25 | — | 未 |
| 编辑账号弹窗（名称/账号/密码/类型） | GameAccounts.vue:34-77 | — | 未 |
| 编辑弹窗 cookie 分支字段（Cookie/Auth 多行） | GameAccounts.vue:48-59 | — | 未 |
| 行内「删除」 | GameAccounts.vue:26 | `ViewModels/AccountItemViewModel.cs:50-51 Delete` | 已移植 |
| 删除确认弹窗 | GameAccounts.vue（Alert showCancel） | — | 未 |
| 「添加账号」按钮 + 弹窗 | GameAccounts.vue:80-130 | `Views/AccountsPage.xaml:57-60` + `Views/AccountsPage.xaml.cs:37-96` | 已移植 |
| 账号类型下拉（4399/4399com/163Email/cookie） | GameAccounts.vue:62-67 | `Views/AccountsPage.xaml.cs:39-51`；`Models/LoginTypeOption.cs` | 已移植 |
| 添加弹窗 cookie 分支（单框 vs 账号+密码） | GameAccounts.vue:94-101 | `AccountsPage.xaml.cs:53-54`（统一「密码 / Cookie」单框） | 部分 |
| 4399 图片验证码弹窗 | GameAccounts.vue:672-713 | `Views/AccountsPage.xaml.cs:120-162` | 已移植 |
| 验证码图片点击刷新 | GameAccounts.vue:674 | `AccountsPage.xaml.cs:129,137-138,164-172` | 已移植 |
| 验证码内容预填（`/captcha4399/content`） | Tools.js:126 | `src/Core/Lanw.Public/Message/AccountMessage.cs:464`（存在，UI 未调用） | 未 |
| 「随机登录」按钮 | GameAccounts.vue:275-285 | `Views/AccountsPage.xaml:61-63` + `AccountsViewModel.cs:202 RandomLoginAsync` | 已移植 |
| Geetest 人机验证 | GameAccounts.vue（Geetest 组件） | `Views/AccountsPage.xaml.cs:180-260`（WebView2） | 已移植 |
| 「自动登录」（autoLogin） | Tools.js:autoLogin | `Views/AccountsPage.xaml:64-66` + `AccountsViewModel.cs:243 AutoLoginLastActiveAsync` | 已移植 |
| 可用账号列表（available） | Tools.js:15-17 | `src/Core/Lanw.Public/Message/AccountMessage.cs:104` | 已移植 |
| 当前账号展示（current） | Tools.js:35-37 | `AccountsViewModel.cs:352 UpdateCurrentDisplay` | 已移植 |
| 切换账号（switch） | Tools.js:30-32 | `AccountsViewModel.cs:300 SwitchToAccount`；`Services/AuthService.cs:73` | 已移植 |
| 注销 / 退出当前登录 | GameAccounts.vue（注销） | `AccountsViewModel.cs:324 Logout` + `Views/AccountsPage.xaml:67-69` | 已移植 |

### 2.4 网络服（Servers.vue / ServerDetail.vue）

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 服务器卡片（图/名称/简介） | Servers.vue:9-17 | `Views/ServersPage.xaml:57-120`；`ViewModels/ServerItemViewModel.cs` | 已移植 |
| 搜索框（名称/简介过滤） | Servers.vue:5-7,83-89 | `Views/ServersPage.xaml:38-40` + `ServersViewModel.cs` | 已移植 |
| 自动批量分页（15/次 + 700ms 循环到空） | Servers.vue:61-81 | `ServersViewModel.cs:127 LoadMoreAsync`（手动按钮，无自动循环） | 部分 |
| 点击卡片进详情 | Servers.vue:91-94 | `Views/ServersPage.xaml:53 ServerList_ItemClick` | 已移植 |
| 错误 Alert 并跳 `/game-accounts` | Servers.vue:21-22,133-138 | `ServersViewModel.cs:107-116`（错误态展示，无跳转） | 部分 |
| 详情：标题 + 服务器 ID | ServerDetail.vue:3-9 | `Views/ServerDetailPage.xaml:19-30` | 已移植 |
| 详情：主图 + 小图点击切换 | ServerDetail.vue:11-17 | `ServerDetailViewModel.cs:147 ShowImage` + `Views/ServerDetailPage.xaml:113` | 已移植 |
| 详情：元信息 4 项（作者/创建时间/版本/地址） | ServerDetail.vue:19-36 | `Views/ServerDetailPage.xaml:128-222`（标签在 :163/:177/:191/:206） | 已移植 |
| 详情：服务器介绍（`v-html` 富文本） | ServerDetail.vue:38-41 | `Views/ServerDetailPage.xaml:230-234`（纯文本） | 部分 |
| 详情：「Launch」按钮 + 弹窗 | ServerDetail.vue:8,44-80 | — | 未 |
| 详情：账号下拉选择（`selectAccount1`） | ServerDetail.vue:51-57 | — | 未 |
| 详情：游戏名称/角色下拉 | ServerDetail.vue:58-64 | — | 未 |
| 详情：「添加名称」+ 保存（创建角色） | ServerDetail.vue:65-68,383-402 | — | 未 |
| 详情：「启动游戏」（`launchGame` net） | ServerDetail.vue:74 | `GameLaunchManagerViewModel.cs:295`（需手工填服务器 ID + 角色名） | 部分 |
| 详情：「启动代理」（`launchProxy` net） | ServerDetail.vue:75 | `ProxyManagerViewModel.cs:203 StartInterceptorAsync`（手工配置转发地址） | 部分 |
| 详情：代理弹窗内插件依赖列表 | ServerDetail.vue:90-100 | — | 未 |
| 详情：「前往论坛反馈」提示 | ServerDetail.vue:47-49 | — | 未 |

### 2.5 皮肤（Skins.vue / SkinDetail.vue）

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 皮肤卡片（图/名/简介/作者/下载/点赞） | Skins.vue:9-22 | `Views/SkinsPage.xaml:115-181` | 已移植 |
| 搜索框（名称） | Skins.vue:5-7,121-132 | `Views/SkinsPage.xaml:41-49` + `SkinsViewModel.cs:193` | 已移植 |
| 搜索结果批量加载至 150 上限 | Skins.vue:158-177 | `SkinsViewModel.cs:234 LoadMore`（手动，无 150 上限与 1200ms 限速） | 部分 |
| 列表批量加载（15/次，1200ms，150 上限） | Skins.vue:135-155 | 同上 | 部分 |
| 点击进详情 | Skins.vue:190-192 | `Views/SkinsPage.xaml:118 SkinList_ItemClick` | 已移植 |
| 错误 Alert | Skins.vue:25-27 | `SkinsViewModel.cs` UploadError/StatusMessage | 部分 |
| 详情：元信息 4 项（作者/发布时间/下载/点赞） | SkinDetail.vue:15-32 | `SkinDetailViewModel.cs:150-153` + `Views/SkinDetailPage.xaml` | 已移植 |
| 详情：皮肤介绍 | SkinDetail.vue:34-37 | `SkinDetailViewModel.cs:154` | 已移植 |
| 详情：「使用皮肤」按钮 + 弹窗 | SkinDetail.vue:8,39-59 | `Views/SkinDetailPage.xaml:210-235`（直接应用，无弹窗） | 部分 |
| 详情：账号下拉 + `switchAccount` | SkinDetail.vue:47-52,132-142 | — | 未 |
| 详情：应用皮肤（`setGameSkin`） | SkinDetail.vue:151-159 | `SkinDetailViewModel.cs:180 ApplyAsync`；`Services/SkinService.cs:75` | 已移植 |
| 详情：确认应用弹窗（alertType=confirm） | SkinDetail.vue:161-176 | `SkinDetailViewModel.cs:190-194`（仅登录校验） | 部分 |

### 2.6 插件（Plugins / PluginStore / PluginDetail）

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 已装插件表格（名称/版本/作者/状态） | Plugins.vue:5-35 | `Views/PluginsPage.xaml:54-109` | 已移植 |
| 启动/停止按钮 | Plugins.vue:27-29 | `Views/PluginsPage.xaml:93-99` + `Services/PluginService.cs:27` | 已移植 |
| 删除按钮 | Plugins.vue:30 | `Views/PluginsPage.xaml:100-105` | 已移植 |
| 删除确认弹窗 | Plugins.vue:39-49 | `Views/PluginsPage.xaml.cs:56-82` | 已移植 |
| 「可能需重启」提示 | Plugins.vue:36 | `Views/PluginsPage.xaml:139-142` | 已移植 |
| 商城卡片（名/简介/发布者/下载次数） | PluginStore.vue:8-19 | `Views/PluginStorePage.xaml:58-150` | 已移植 |
| 商城搜索（名/简介/发布者） | PluginStore.vue:5-7,44-51 | `Views/PluginStorePage.xaml:33-38` | 已移植 |
| 商城点击进详情 | PluginStore.vue:53-55 | `Views/PluginStorePage.xaml:60 Plugin_ItemClick` | 已移植 |
| 商城分页加载更多 | PluginStore.vue:40-42 | `Views/PluginStorePage.xaml:152` + `Services/PluginService.cs:43` | 已移植 |
| 详情：标题 + 插件 ID | PluginDetail.vue:6-12 | `Views/PluginDetailPage.xaml:28-41` | 已移植 |
| 详情：元信息 4 项 | PluginDetail.vue:23-52 | `Views/PluginDetailPage.xaml:54-140` | 已移植 |
| 详情：依赖项链接（点击跳转） | PluginDetail.vue:40-51 | `Views/PluginDetailPage.xaml:133 Dependency_Click` | 已移植 |
| 详情：Logo 图 | PluginDetail.vue:19-21 | `PluginDetailViewModel.cs:52,223 LogoUrl/HasLogo` | 已移植 |
| 详情：插件介绍（`v-html`） | PluginDetail.vue:54-59 | `Views/PluginDetailPage.xaml:142-170`（纯文本，注释已说明） | 部分 |
| 详情：「Download」+ 确认弹窗（含依赖清单） | PluginDetail.vue:11-15,129-138 | `Views/PluginDetailPage.xaml:42-48` + `PluginDetailPage.xaml.cs Download_Click` | 部分 |
| 详情：安装状态弹窗 | PluginDetail.vue:16-17,141-157 | `PluginDetailPage.xaml.cs`（状态写入 StatusMessage） | 部分 |
| 详情：加载失败 4 秒倒计时跳商城 | PluginDetail.vue:114-126 | `Views/PluginDetailPage.xaml:180-186`（错误态 + 重试，无倒计时） | 部分 |

### 2.7 代理 / 启动管理

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 代理表格（ID/昵称/本地地址/服务器名） | ProxyManager.vue:15-47 | `Views/ProxyManagerPage.xaml:131-250` | 已移植 |
| 「关闭全部代理」按钮 | ProxyManager.vue:6-8 | `Views/ProxyManagerPage.xaml:166` | 已移植 |
| 关闭全部确认弹窗 | ProxyManager.vue:57-58,124-129 | — | 未 |
| 行内「复制」本地地址 | ProxyManager.vue:34-38 | `Views/ProxyManagerPage.xaml:236` + `ProxyItemViewModel.cs:62` | 已移植 |
| 行内「关闭」 | ProxyManager.vue:39-41 | `Views/ProxyManagerPage.xaml:237` + `ProxyItemViewModel.cs:72` | 已移植 |
| 关闭单个确认弹窗 | ProxyManager.vue:116-121 | — | 未 |
| 运行中代理数量统计 | ProxyManager.vue:11-13 | `ProxyManagerViewModel.cs:189 ProxyCount` | 已移植 |
| 复制成功提示（2 秒自动隐藏） | ProxyManager.vue:54,73-85 | `ProxyManagerViewModel.cs` StatusMessage | 已移植 |
| 空态 / 加载态 | ProxyManager.vue:49-52 | `Views/ProxyManagerPage.xaml` EmptyVisibility | 已移植 |
| 游戏实例表格（角色/游戏/UserId/版本） | GameLaunchManager.vue:16-41 | `Views/GameLaunchManagerPage.xaml:146-173` | 已移植 |
| 「关闭全部游戏」按钮 | GameLaunchManager.vue:6-8 | `Views/GameLaunchManagerPage.xaml:143` | 已移植 |
| 关闭全部确认弹窗 | GameLaunchManager.vue:96-101 | — | 未 |
| 行内「关闭」 | GameLaunchManager.vue:34-36 | `Views/GameLaunchManagerPage.xaml:166-169` | 已移植 |
| 关闭单个确认弹窗 | GameLaunchManager.vue:88-93 | — | 未 |
| 实例数量统计 | GameLaunchManager.vue:11-13 | `Views/GameLaunchManagerPage.xaml:138 InstanceSummary` | 已移植 |
| 空态 / 加载态 | GameLaunchManager.vue:44-47 | `Views/GameLaunchManagerPage.xaml:175-178` | 已移植 |

### 2.8 租赁服（GameRental / GameRentalDetail）

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 列表卡片（图/名/简介/版本/在线） | GameRental.vue:9-21 | `Views/GameRentalPage.xaml:57-190` | 已移植 |
| 搜索框 | GameRental.vue:5-7,80-86 | `Views/GameRentalPage.xaml:38-40` | 已移植 |
| 进入页面先排序（`sortRentalServer`） | GameRental.vue:49 | `src/App/Lanw.App/Services/RentalGameService.cs:41` | 已移植 |
| 批量分页加载（15/次 + 700ms 循环） | GameRental.vue:58-78 | `GameRentalViewModel.cs:127 LoadMoreAsync`（手动） | 部分 |
| 点击进详情 | GameRental.vue:88-90 | `Views/GameRentalPage.xaml:53 RentalList_ItemClick` | 已移植 |
| 错误 Alert + 跳转 | GameRental.vue:24-26,124-129 | `GameRentalViewModel.cs:202`（错误态，无跳转） | 部分 |
| 详情：标题 + 服务器 ID | GameRentalDetail.vue:3-9 | `Views/GameRentalDetailPage.xaml:18-30` | 已移植 |
| 详情：元信息 4 项（版本/类型/在线/地址） | GameRentalDetail.vue:15-32 | `GameRentalDetailViewModel.cs:259 Apply` | 已移植 |
| 详情：服务器介绍 | GameRentalDetail.vue:34-37 | `Views/GameRentalDetailPage.xaml:344-355` | 已移植 |
| 详情：「Launch」按钮 + 弹窗 | GameRentalDetail.vue:8,39-76 | — | 未 |
| 详情：账号下拉 | GameRentalDetail.vue:47-53 | — | 未 |
| 详情：角色下拉 | GameRentalDetail.vue:54-60 | — | 未 |
| 详情：「添加名称」（`addRentalRole`） | GameRentalDetail.vue:238-249 | — | 未 |
| 详情：「启动游戏」（`launchGame` rental） | GameRentalDetail.vue:70 | `GameLaunchManagerViewModel.cs:295`（手工参数） | 部分 |
| 详情：「启动代理」（`launchProxy` rental） | GameRentalDetail.vue:71,229 | `ProxyManagerViewModel.cs:203`（手工配置 + is_rental 开关） | 部分 |
| 详情：连接地址（三线接入） | GameRentalDetail.vue:28-31 | `GameRentalDetailViewModel.cs:272 ApplyAddress`、`:288 AddAddressLine` | 已移植 |
| 详情：复制地址 | （源无独立按钮） | `GameRentalDetailViewModel.cs:195 CopyAddress` | 已移植（增强） |
| 详情：玩家/角色列表 | GameRentalDetail.vue:getlaunch | `ViewModels/GameRentalDetailViewModel.cs:233 LoadPlayersAsync`；`Services/RentalGameService.cs:76` | 已移植 |

### 2.9 用户中心 / 设置 / 日志 / 版本

| 功能点 | 源位置 | lanw 位置 | 状态 |
|---|---|---|---|
| 剩余天数展示 | UserHome.vue:26 | `Views/UserHomePage.xaml:64-67 DaysText` | 已移植 |
| 「隐藏账号」checkbox | UserHome.vue:15-23 | `Views/UserHomePage.xaml:59-62` + `UserHomeViewModel.cs:206` | 已移植 |
| 「退出登录」 | UserHome.vue:5,109-122 | `Views/UserHomePage.xaml:38-42 Logout_Click` | 已移植 |
| 购买天数外链 | UserHome.vue:28-30 | `Views/UserHomePage.xaml:83-86` | 已移植 |
| 「关于高级功能」提示 | UserHome.vue:31-34 | `Views/UserHomePage.xaml:92-98` | 已移植 |
| 加载失败倒计时跳 `/login` | UserHome.vue:61-66,125-139 | `UserHomeViewModel.cs:235 NotLoggedIn` | 部分 |
| Loading 组件（倒计时） | UserHome.vue:42-43 | — | 部分 |
| 设置：开启主动登录 | Settings.vue:12-19 | `Views/SettingsPage.xaml:34-36` | 已移植 |
| 设置：开启主动登录 Cookie | Settings.vue:21-28 | `Views/SettingsPage.xaml:37-39` | 已移植 |
| 设置：开启主动登录 163Email | Settings.vue:30-37 | `Views/SettingsPage.xaml:40-42` | 已移植 |
| 设置：Chat \| IRC 是否开启 | Settings.vue:42-54 | `Views/SettingsPage.xaml:57-59` | 已移植 |
| 设置：使用 JavaW | Settings.vue:60-67 | `Views/SettingsPage.xaml:74-76` | 已移植 |
| 设置：游戏内存滑块（1G–18G，step 512） | Settings.vue:69-76 | `Views/SettingsPage.xaml:80-84` | 已移植 |
| 设置：虚拟机参数 | Settings.vue:78-81 | `Views/SettingsPage.xaml:88-92` | 已移植 |
| 设置：游戏参数 | Settings.vue:83-86 | `Views/SettingsPage.xaml:96-100` | 已移植 |
| 设置：自动更新插件 | Settings.vue:93-100 | `Views/SettingsPage.xaml:118-120` | 已移植 |
| 设置：改动即保存（无保存按钮） | Settings.vue（各 watch → API） | `Views/SettingsPage.xaml:131-140`（需点「保存设置」） | 部分 |
| 日志列表 + 级别着色 | Logs.vue:21-28,42-44 | `ViewModels/LogItemViewModel.cs` + `Views/LogsPage.xaml:57-70` | 已移植 |
| 日志空态 | Logs.vue:38-40 | `Views/LogsPage.xaml:72-77` | 已移植 |
| 日志「加载中...」 | Logs.vue:34-36 | — | 未 |
| 版本提示「当前版本可能不包含此内容」 | Version.vue:1-6 | `Views/VersionPage.xaml:28-35` | 已移植 |

---

## 3. 后端接口 / 动作覆盖表

> 源端点均为 `Fantnel/Servlet/**` 下的 ASP.NET Core Controller 动作；lanw 已无 HTTP 层，等价物为「进程内直调的类/方法」。

### 3.1 账号 —— `GameAccountController.cs`

| 源端点 | 源动作（行） | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `GET /api/gameaccount/get` | `GetAccountHttp` (18) | `AccountMessage.GetAccountList` | 已移植 | `src/Core/Lanw.Public/Message/AccountMessage.cs:114` |
| `GET /api/gameaccount/available` | `GetAccountAvailableHttp` (25) | `AccountMessage.GetLoginAccountList` | 已移植 | `AccountMessage.cs:104` |
| `GET /api/gameaccount/captcha4399` | `GetCaptcha4399Http` (32) | `AccountMessage.UpdateCaptcha` | 已移植 | `AccountMessage.cs:42`；UI `Services/AuthService.cs:52` |
| `POST /api/gameaccount/captcha4399/verify` | `VerifyCaptcha4399Http` (39) | `AccountMessage.LoginWithCaptcha4399` | 已移植 | `AccountMessage.cs:394` |
| `GET /api/gameaccount/captcha4399/content` | `GetCaptcha4399ContentHttp` (46) | `AccountMessage.GetCaptcha4399Content` | 部分 | `AccountMessage.cs:464` 存在，但只有 `AutoLogin1`(:369) 内部使用，UI 无预填 |
| `GET /api/gameaccount/select?id` | `SelectAccount` (53) | `AccountMessage.Login` | 已移植 | `AccountMessage.cs:165` |
| `POST /api/gameaccount/autoLogin` | `AutoLoginHttp` (66) | `AccountMessage.AutoLogin1` | 已移植 | `AccountMessage.cs:364`；`Services/AuthService.cs:93` |
| `GET /api/gameaccount/delete?id` | `DeleteAccountHttp` (78) | `AccountMessage.DeleteAccount` | 已移植 | `AccountMessage.cs:435` |
| `POST /api/gameaccount/save` | `SaveAccountHttp` (91) | `AccountMessage.SaveAccount` | 已移植 | `AccountMessage.cs:267` |
| `POST /api/gameaccount/update` | `UpdateAccountHttp` (98) | `AccountMessage.UpdateAccount` | 部分 | `AccountMessage.cs:243` 存在；无 UI 入口（`Services/AccountRepository.cs:49` 亦无调用） |
| `GET /api/gameaccount/switch?id` | `SwitchAccountHttp` (105) | `AccountMessage.SwitchAccount` | 已移植 | `AccountMessage.cs:75`；`Services/AuthService.cs:73` |
| `POST /api/gameaccount/autoswitch` | `AutoSwitchAccountHttp` (112) | `AccountMessage.AutoSwitchAccount` | 已移植 | `AccountMessage.cs:375`；`Services/AuthService.cs:83` |
| `GET /api/gameaccount/current` | `GetGameAccountHttp` (119) | `InfoManager.GetGameAccount` | 已移植 | `src/Core/Lanw.Core/Manager/InfoManager.cs`；`Services/AuthService.cs:22` |
| `POST /api/gameaccount/random` | `RandomAccountHttp` (125) | `AccountMessage.RandomAccount` | 已移植 | `AccountMessage.cs:484` |

### 3.2 启动 / 代理 —— `GameLaunchController.cs` + `GameProxiesController.cs`

| 源端点 | 源动作（行） | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `GET /api/gamelaunch/launch` | `LaunchGame` (12) | `GameLaunchService.LaunchAsync` | 部分 | `src/App/Lanw.App/Services/GameLaunchService.cs:88`。**缺**：源 `LaunchMessage.LaunchGame`（`_fantnel_ref/Nirvana.Public/Message/LaunchMessage.cs:24`）内的 ① Java 自动安装 `ExEnvironment`(:133) ② 服务器详情取版本(:93) ③ 角色校验 `ServersGameMessage.GetUserName`(:103) ④ 服务器地址解析(:109) ⑤ 旧实例清理 `ActiveGameAndProxies.Close(id,name)`(:86) |
| `GET /api/gamelaunch/get` | `GetLauncherService` (19) | `GameLaunchService.Instances` | 已移植 | `GameLaunchService.cs:76` |
| `GET /api/gamelaunch/close?id` | `CloseGame` (26) | `GameLaunchService.Close` | 已移植 | `GameLaunchService.cs:159` |
| `GET /api/gameserver/launch` | `LaunchGame` (16) | `ProxyService.Start` | 部分 | `src/App/Lanw.App/Services/ProxyService.cs:100`。**缺**：源 `ProxiesMessage.StartProxyAsync`（`_fantnel_ref/Nirvana.Public/Message/ProxiesMessage.cs:21`）内的 ① 服务器详情/地址 ② **核心模组安装 `InstallerService.InstallGameMods`**(:62) ③ 角色校验(:67) ④ `PluginMessage.InitializeAuto()`(:77) ⑤ 自动取空闲端口(:27) |
| `GET /api/server/get` | `GetLaunchHttp` (23) | 代理列表已移植；本机 IP 未展示 | 部分 | `ProxyService.cs:87 GetAllProxies`；`src/Core/Lanw.Core/Utils/Tools.cs:125 GetLocalIpAddress`（无 UI 调用） |
| `GET /api/server/close?id` | `CloseGame` (35) | `ProxyService.Close` | 已移植 | `ProxyService.cs:147` |
| `POST /api/gameproxie/authenticator` | `LaunchGameProxy` (42) | `NetEaseConnection.CreateAuthenticator` | 部分 | `src/Core/Lanw.Cipher/Cipher/Nirvana/Connection/NetEaseConnection.cs:53` 存在；`ProxyService.Start` 传 `null` 会话回调（`ProxyService.cs:115-116`），无 UI/流程调用 |

### 3.3 网络服 —— `GameServerController.cs`

| 源端点 | 源动作（行） | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `GET /api/gameserver/get` | `GetServerHttp` (13) | `ServerService.GetServerListAsync` | 已移植 | `src/App/Lanw.App/Services/ServerService.cs:21` |
| `GET /api/gameserver/id` | `GetIdServerHttp` (20) | `ServerService.GetServerDetailAsync` | 已移植 | `ServerService.cs:28` |
| `GET /api/gameserver/getlaunch` | `GetServerInfo` (27) | `NPFLauncher.GetNetGameCharactersAsync` | 部分 | `src/Core/Lanw.WPFLauncher/Protocol/NPFLauncher.cs:187` 存在，**src/App 内无任何调用**（角色列表 UI 缺失） |
| `POST /api/gameserver/createname` | `CreateGameName` (43) | `NPFLauncher.CreateCharacterAsync` + `ServersGameMessage.GetUserName` | 部分 | `NPFLauncher.cs:202`、`src/Core/Lanw.Public/Message/ServersGameMessage.cs:200` 存在，**src/App 内无调用**（「添加名称」UI 缺失） |

### 3.4 皮肤 —— `GameSkinController.cs`

| 源端点 | 源动作（行） | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `GET /api/gameskin/get` | `GetServerHttp` (12) | `SkinMessage.GetSkinList` / `GetSkinListByName` | 已移植 | `src/Core/Lanw.Public/Message/SkinMessage.cs:27,131`；`Services/SkinService.cs:51,60` |
| `GET /api/gameskin/detail` | `GetSkinDetailHttp` (19) | `EntitySkinDetail` | 已移植 | `src/Core/Lanw.Public/Entities/NEL/EntitySkinDetail.cs`；`SkinService.cs:67` |
| `GET /api/gameskin/set?id` | `SetSkinHttp` (26) | `NPFLauncher.SetSkinAsync` | 已移植 | `src/Core/Lanw.WPFLauncher/Protocol/NPFLauncher.Skin.cs:55`；`SkinService.cs:75` |

### 3.5 租赁服 —— `GameRentalController.cs`

| 源端点 | 源动作（行） | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `GET /api/gamerental/get` | `GetRentalGameListHttp` (13) | `RentalGameMessage.GetServerList` | 已移植 | `src/Core/Lanw.Public/Message/RentalGameMessage.cs:28`；`Services/RentalGameService.cs:36` |
| `GET /api/gamerental/sort` | `GetRentalGameSortHttp` (20) | `RentalGameMessage.SortServerList` | 已移植 | `RentalGameMessage.cs:78`；`Services/RentalGameService.cs:41` |
| `GET /api/gamerental/getlaunch` | `GetRentalInfo` (27) | `NPFLauncher.GetRentalGameRolesListAsync` | 部分 | `NPFLauncher.cs:282` 存在；`RentalGameService.cs:77` 仅用于「玩家列表」展示，**无「账号+角色」合并选择启动视图** |
| `GET /api/gamerental/id` | `GetIdServerHttp` (42) | `EntityRentalDetail`（类未移植） | 部分 | 源 `_fantnel_ref/Nirvana.Public/Entities/NEL/EntityRentalDetail.cs:13`；lanw 无同名文件，行为在 `GameRentalDetailViewModel.cs:138-179`（详情→地址→玩家串行重组，源为并行 + `CacheManager.ClearCacheImage`） |
| `POST /api/gamerental/createname` | `CreateGameName` (49) | `NPFLauncher.CreateCharacterRental` + `RentalGameMessage.GetUserName` | 部分 | `NPFLauncher.cs:298`、`RentalGameMessage.cs:115` 存在，**src/App 内无调用**（「添加名称」UI 缺失） |

### 3.6 插件 —— `PluginsListController.cs` + `PluginsShopController.cs`

| 源端点 | 源动作（行） | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `GET /api/plugins/get` | `GetPluginsListHttp` (13) | `PluginManager.GetPluginStates` | 已移植 | `src/App/Lanw.App/Services/PluginService.cs:23` |
| `GET /api/plugins/toggle?id` | `TogglePluginHttp` (20) | `PluginManager.TogglePlugin` | 已移植 | `PluginService.cs:27` |
| `GET /api/plugins/delete?id` | `DeletePluginHttp` (27) | `PluginManager.DeletePlugin` | 已移植 | `PluginService.cs:35` |
| `GET /api/plugins/dependence` | `GetDependenceListHttp` (36) | `PluginMessage.GetDependenceList` | 已移植 | `src/Core/Lanw.Public/Message/PluginMessage.cs:38`；`PluginService.cs:55` |
| `GET /api/pluginstore/get` | `GetPluginListHttp` (11) | `PlugInstoreMessage.GetPluginList` | 已移植 | `src/Core/Lanw.Public/Message/PlugInstoreMessage.cs:21`；`PluginService.cs:43` |
| `GET /api/pluginstore/detail` | `GetPluginDetailHttp` (18) | `PlugInstoreMessage.GetPluginDetail` | 已移植 | `PlugInstoreMessage.cs:67`；`PluginService.cs:47` |
| `GET /api/pluginstore/install` | `DownloadHttp` (25) | `PlugInstoreMessage.Install` | 已移植 | `PlugInstoreMessage.cs:176`；`PluginService.cs:51` |

### 3.7 其它 —— `FantController.cs` + `HomeController.cs` + `NirvanaController.cs`

| 源端点 | 源动作（行） | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `GET /api/version` | `GetVersion` (16) | `LanwProgram.Version/VersionId/Mode/Arch` | 已移植 | `src/Core/Lanw.Core/LanwProgram.cs:13-26`；`ViewModels/VersionViewModel.cs:60` |
| `GET /api/reboot` | `Reboot` (29) | `Tools.Restart` | 未移植 | 方法在 `src/Core/Lanw.Core/Utils/Tools.cs:161`，**无 UI 调用点** |
| `GET /api/exit` | `Exit` (38) | — | N/A | 桌面端由关闭窗口替代；`ActiveGameAndProxies.CloneAll` 语义不适用 |
| `GET /api/logs` | `GetLogs` (48) | `InMemorySink.GetLogs` | 已移植 | `src/App/Lanw.App/Services/ViewLogService.cs:48` |
| `GET /api/theme/set?name` | `SetTheme` (16) | `ThemeMessage.SetTheme` | 已移植 | `src/Core/Lanw.Public/Message/ThemeMessage.cs:26` |
| `GET /api/theme` | `GetTheme` (24) | `ThemeMessage.GetTheme` | 部分 | `ThemeMessage.cs:23` 已移植，但**无 UI 消费方**（源为 `App.vue` 主题初始化，lanw 改为跟随系统 `Services/ThemeService.cs:15`） |
| `GET /api/home` | `HomeInfo` (33) | `InfoManager.FantnelInfo` | 已移植 | `src/Core/Lanw.Public/Message/HomeMessage.cs:23` |
| `POST /api/theme/switch` | `ThemeSwitch` (40) | `ThemeMessage.SwitchThemeAsync`（含 `SafeThemeAsync` + `EntityUpdate.CheckUpdateSafe`） | 已移植 | `ThemeMessage.cs:37-54`（`SafeThemeAsync` :29）；UI `ViewModels/HomeViewModel.cs:149` |
| `GET /api/nirvana/login` | `Login` (13) | `NirvanaAccountManager.Login` | 已移植 | `UserHomeViewModel.cs:162` |
| `GET /api/nirvana/logout` | `Logout` (21) | `LanwConfig.Logout` | 已移植 | `UserHomeViewModel.cs:196` |
| `GET /api/nirvana/account/get` | `GetAccount` (29) | `NirvanaAccountManager.GetLoginInfo` | 已移植 | `UserHomeViewModel.cs:164` |
| `GET /api/nirvana/add` | `AddConfig` (37) | `LanwConfig.AddByTypeName` | 已移植 | `UserHomeViewModel.cs:217` 等 |
| `GET /api/nirvana/set` | `SetConfig` (45) | `LanwConfig.SetValue` / `SetGameMemory` / `SetChatEnable` | 已移植 | `ViewModels/SettingsViewModel.cs:95-104`；`ChatViewModel.cs:46` |
| `GET /api/nirvana/get` | `GetConfig` (60) | `LanwConfig.GetValue` | 已移植 | `SettingsViewModel.cs:134-143` |
| `GET /api/test` | `Test` (10) | — | N/A | 调试端点，桌面端无需 |

### 3.8 启动期初始化与全局业务动作（`Nirvana.Public/Utils/InitProgram.cs`）

| 动作 | 源位置 | lanw 类/方法 | 状态 | 证据 |
|---|---|---|---|---|
| `FantnelInit`：拉 `/fantnel.json` + 填 `CrcSalt` | InitProgram.cs:159-195 | `HomeMessage.GetHomeInfoAsync` + `X19.CrcSalt` | 已移植 | `HomeMessage.cs:23`；`ViewModels/HomeViewModel.cs:105` |
| 版本安全检测（禁用版本 → `Environment.Exit(1)`） | InitProgram.cs:45,92-132 | — | 未移植 | 无调用点；`LanwProgram.LatestVersion` 恒为 `true`（`LanwProgram.cs:17`） |
| **插件管理器初始化 `PluginMessage.Initialize()`** | InitProgram.cs:58 | `PluginMessage.Initialize` | 未移植 | 方法在 `src/Core/Lanw.Public/Message/PluginMessage.cs:15`，**无任何调用点**（`App.xaml.cs` 未调用） |
| `PluginMessage.InitializeAuto()`（启动代理前插件热加载） | ProxiesMessage.cs:77,118 | `PluginMessage.InitializeAuto` | 未移植 | `PluginMessage.cs:24` 存在，无调用点 |
| `CacheManager.CacheServer()` 服务器/图片缓存预热 | InitProgram.cs:72 | `CacheManager.CacheServer` | 未移植 | 方法在 `src/Core/Lanw.Public/Manager/CacheManager.cs:34`，无调用点 |
| 皮肤 / 租赁服 图片缓存（`GetCacheImageUrl` / `ClearCacheImage` 重载） | CacheManager.cs:95,112,184,190 | — | 未移植 | lanw `CacheManager.cs` 只有 `EntityNetGameItem` 一个重载；`SkinMessage.cs:98,121`、`RentalGameMessage.cs:96`、`EntitySkinDetail.cs:60` 均为注释占位 |
| 在线心跳 `/api/tick`（每 180 秒） | InitProgram.cs:135-157 | — | 未移植 | 无调用点 |
| `StandardYggdrasil.InitializationAsync()` 预取验证服务器 | InitProgram.cs:79 | — | 未移植 | 类在 `src/Core/Lanw.Cipher/Yggdrasil/StandardYggdrasil.cs`，无调用点 |
| 启动时检查更新 `UpdateTools.CheckUpdate` | InitProgram.cs:29 | `VersionService.CheckAllAsync` | 部分 | `src/App/Lanw.App/Services/VersionService.cs:92`（仅手动触发，非启动时） |
| **Java 运行时自动安装 `ExEnvironment`** | LaunchMessage.cs:133-180 | — | 未移植 | lanw 仅 `GameLaunchService.cs:263 WarnIfJavaMissing`（只告警，不下载解压） |
| 核心模组安装 `InstallerService.InstallGameMods` | LaunchMessage.cs（经 LauncherService）/ ProxiesMessage.cs:62 | `InstallerService.InstallGameMods` | 已移植 | `src/Core/Lanw.Game.Launcher/Services/Java/InstallerService.cs:84`；经 `LauncherService.cs:100` 接线。**注意**：仅白端启动路径接线，代理路径未接线 |

---

## 4. 未移植 / 部分移植清单（按优先级排序）

> 本节是**合并后的可执行待办**（30 条）：把 §2/§3 中细粒度的功能点按「同一落点/同一改动」合并成一条，便于排期。
> **计数以 §2 / §3 为准**（未移植 33 项、部分移植 38 项）；本节条数少于计数是因为多条功能点归属同一缺口（例：SD-5/SD-6/SD-7 三条并入 P0#4）。

> 工作量估计：**小** ≤ 0.5 人日；**中** 1–3 人日；**大** ≥ 4 人日。

### P0 — 阻断「核心玩法」（能看见但用不了）

| # | 缺口 | 类型 | 源位置 | 建议落点 | 工作量 |
|---|---|---|---|---|---|
| 1 | **Java 运行时自动安装缺失**：无 Java 环境时只写日志告警，用户无法启动游戏且无自助修复路径 | 未移植 | `LaunchMessage.cs:133-180 ExEnvironment` | `src/App/Lanw.App/Services/GameLaunchService.cs` 的 `LaunchAsync` 前插入，或新建 `src/Core/Lanw.Public/Message/LaunchMessage.cs` 承载 | 中 |
| 2 | **`LaunchMessage` / `ProxiesMessage` 两个 Message 类整体缺失**，启动/代理的「服务器详情→版本→角色校验→地址→旧实例清理」编排逻辑散落到 App 层且不完整 | 未移植 | `Nirvana.Public/Message/LaunchMessage.cs`、`ProxiesMessage.cs` | `src/Core/Lanw.Public/Message/LaunchMessage.cs`、`ProxiesMessage.cs`（与移植约定「后端逻辑放 Message」一致） | 大 |
| 3 | **启动期插件管理器未初始化**：`PluginMessage.Initialize()` 无调用点，装的插件不会被加载/注册，插件页开关只是改状态 | 未移植 | `InitProgram.cs:58` | `src/App/Lanw.App/App.xaml.cs:60` 附近（`LanwConfig.Initialization()` 之后） | 小 |
| 4 | **服务器详情页整套 Launch 弹窗缺失**（Launch 按钮 / 账号下拉 / 角色下拉 / 启动游戏 / 启动代理 / 插件依赖提示） | 未移植 | `ServerDetail.vue:8,44-100` | `Views/ServerDetailPage.xaml` + `ViewModels/ServerDetailViewModel.cs`。底层 API 已就绪：`NPFLauncher.GetNetGameCharactersAsync`(`NPFLauncher.cs:187`)、`CreateCharacterAsync`(:202) | 大 |
| 5 | **租赁服详情页整套 Launch 弹窗缺失**（同 #4） | 未移植 | `GameRentalDetail.vue:8,39-76` | `Views/GameRentalDetailPage.xaml` + `GameRentalDetailViewModel.cs`。底层：`GetRentalGameRolesListAsync`(`NPFLauncher.cs:282`)、`CreateCharacterRental`(:298) | 大 |
| 6 | **启动代理不走服务器上下文**：不取服务器详情/地址、**不装核心模组**、不校验角色、不热加载插件；用户必须手抄转发地址与端口 | 部分 | `ProxiesMessage.cs:45-88` | `src/App/Lanw.App/Services/ProxyService.cs:100 Start` + 新增 `ProxiesMessage` | 大 |

### P1 — 高频日常操作缺环

| # | 缺口 | 类型 | 源位置 | 建议落点 | 工作量 |
|---|---|---|---|---|---|
| 7 | **「添加名称」（创建游戏角色）缺失**（网络服 + 租赁服两处） | 未移植 | `ServerDetail.vue:65-68,383-402`；`GameRentalDetail.vue:238-249` | 随 #4/#5 一起做；底层 `CreateCharacterAsync`/`CreateCharacterRental` 已就绪 | 中 |
| 8 | **账号「编辑」功能完全缺失**：`AccountMessage.UpdateAccount`(`AccountMessage.cs:243`) 与 `AccountRepository.Update`(`AccountRepository.cs:49`) 都存在但无 UI 入口 | 未移植 | `GameAccounts.vue:25,34-77` | `Views/AccountsPage.xaml.cs` 加「编辑」弹窗 + `AccountsViewModel` 加 `UpdateAccountCommand` | 中 |
| 9 | **账号删除无二次确认**（源有 Alert showCancel），误删不可恢复 | 未移植 | `GameAccounts.vue`（Alert showCancel） | `Views/AccountsPage.xaml.cs` 加 `ContentDialog`（可照抄 `PluginsPage.xaml.cs:56-82`） | 小 |
| 10 | **代理/游戏实例关闭无二次确认**（源两处均有确认弹窗） | 未移植 | `ProxyManager.vue:116-129`；`GameLaunchManager.vue:88-101` | `Views/ProxyManagerPage.xaml.cs`、`GameLaunchManagerPage.xaml.cs` | 小 |
| 11 | **列表批量加载退化为手动「加载更多」**（服务器/皮肤/租赁服三页），丢失源的自动循环与限速 | 部分 | `Servers.vue:61-81`；`Skins.vue:135-177`；`GameRental.vue:58-78` | 三个 VM 的 `LoadAsync` 后追加自动循环（含 700/1200ms 间隔与 150 上限） | 中 |
| 12 | **缓存预热缺失**：`CacheManager.CacheServer()` 无调用点 → 首屏图片全靠实时下载 | 未移植 | `InitProgram.cs:68-76` | `App.xaml.cs` 启动后台任务（需同时修 #13） | 小 |
| 13 | **皮肤/租赁服图片缓存未移植**：`GetCacheImageUrl` 只有 net 重载，皮肤与租赁服 Message 内为注释占位 | 未移植 | `CacheManager.cs:95,112,184,190` | `src/Core/Lanw.Public/Manager/CacheManager.cs` 补两个重载；`SkinMessage.cs:98,121`、`RentalGameMessage.cs:96`、`EntitySkinDetail.cs:60` 打开调用 | 中 |
| 14 | **登录页被并入用户中心**，丢失独立路由与「已登录自动跳转」语义；未登录时用户中心同时承担登录与信息展示 | 部分 | `NirvanaLogin.vue` / `NirvanaLoginSocket.vue` | 视产品决定是否需要独立 `LoginPage`；至少补齐启动登录守卫 | 中 |

### P2 — 体验与一致性

| # | 缺口 | 类型 | 源位置 | 建议落点 | 工作量 |
|---|---|---|---|---|---|
| 15 | **三态主题切换降级为「跟随系统」**：源有亮/暗/灰三态切换（`GET /api/theme/set`），lanw 恒为跟随系统。~~主题文件导入~~ 已在审计期间并行落地（`ThemeMessage.cs` + `ThemeFilePicker.cs`），不再计入本项 | 部分 | `App.vue:36-43`；`HomeController.cs:16` | `Services/ThemeService.cs:15` 扩展为可显式设 `ElementTheme`；`Views/ShellPage.xaml` 加主题切换按钮 + `ThemeMessage.GetTheme/SetTheme`（`ThemeMessage.cs:23,26`） | 中 |
| 16 | **设置项改为「点保存才生效」**，源为改动即存；用户容易改完忘保存 | 部分 | `Settings.vue`（各 watch → API） | `ViewModels/SettingsViewModel.cs` 各 setter 内即时持久化 | 小 |
| 17 | **富文本不渲染**：服务器介绍 / 插件介绍在源为 `v-html`，lanw 为纯文本（含 HTML 标签会原样显示） | 部分 | `ServerDetail.vue:40`；`PluginDetail.vue:57` | 引入轻量 HTML→TextBlock/WebView2 渲染 | 中 |
| 18 | **插件安装确认弹窗未列出依赖清单**（源会拼「并安装 X 依赖」文案），用户不知会连带装什么 | 部分 | `PluginDetail.vue:129-138` | `Views/PluginDetailPage.xaml.cs Download_Click` | 小 |
| 19 | **验证码内容预填未接线**：`GetCaptcha4399Content`(`AccountMessage.cs:464`) 存在，UI 未调用（源可预填输入框） | 部分 | `Tools.js:126` | `Views/AccountsPage.xaml.cs:120-162` 弹窗打开时预填 | 小 |
| 20 | **详情页错误无自动跳转**：源多处失败后倒计时跳转（`/game-accounts`、`/plugin-store`、`/login`），lanw 只显示错误态 | 部分 | `Servers.vue:133-138`；`PluginDetail.vue:114-126`；`UserHome.vue:125-139` | 各 VM 错误分支加跳转 | 小 |
| 21 | **日志页缺「加载中」态** | 未移植 | `Logs.vue:34-36` | `Views/LogsPage.xaml` | 小 |
| 22 | **在线心跳 `/api/tick` 未移植**，服务端无法统计活跃桌面端 | 未移植 | `InitProgram.cs:135-157` | `App.xaml.cs` 后台循环 | 小 |
| 23 | **版本安全检测未移植**：源会拒绝并退出已禁用版本，lanw `LatestVersion` 恒 `true` | 未移植 | `InitProgram.cs:92-132` | `App.xaml.cs` 启动校验 + `LanwProgram.LatestVersion` 落地 | 中 |
| 24 | **`StandardYggdrasil` 预取未接线**，首次鉴权可能首包超时 | 未移植 | `InitProgram.cs:79` | `App.xaml.cs` 启动后台任务 | 小 |
| 25 | **重启程序入口缺失**（`/api/reboot`）；`Tools.Restart` 已存在但无 UI | 未移植 | `FantController.cs:29` | `Views/SettingsPage.xaml` 加「重启应用」 | 小 |
| 26 | **`/api/server/get` 的本机 IP 未展示**，代理页用户看不到该把哪个地址给别人 | 部分 | `GameProxiesController.cs:23-33` | `ViewModels/ProxyManagerViewModel.cs` 展示 `Tools.GetLocalIpAddress`(`Tools.cs:125`) | 小 |
| 27 | **`gamerental/getlaunch` 的「账号+角色」合并视图缺位**（后端能力在，仅用于玩家列表展示） | 部分 | `GameRentalController.cs:27-41` | 随 #5 一起做 | 中 |
| 28 | **`EntityRentalDetail` 类未移植**，其「详情+地址并行聚合 + 清缓存」语义被拆到 VM 串行实现（语义等价性未确认） | 部分 | `Nirvana.Public/Entities/NEL/EntityRentalDetail.cs:13` | `src/Core/Lanw.Public/Entities/NEL/EntityRentalDetail.cs` | 小 |
| 29 | **`gameproxie/authenticator` 未接线**：`NetEaseConnection.CreateAuthenticator`(`NetEaseConnection.cs:53`) 存在，但 `ProxyService.Start` 传 `null` 会话回调 | 部分 | `GameProxiesController.cs:42` | `Services/ProxyService.cs:115` 注入 `serverId` 回调 | 中 |
| 30 | **皮肤详情页缺「账号选择」**：源可在应用皮肤前下拉切换账号（`switchAccount` + `getAvailableAccounts`），lanw 只能用「当前账号」，选错账号只能先去账号页切换 | 未移植 | `SkinDetail.vue:47-52,112-142` | `Views/SkinDetailPage.xaml` + `SkinDetailViewModel.cs`；后端 `Services/AuthService.cs:73` 已就绪 | 小 |

### 设计性废弃（不建议补）

| 缺口 | 源位置 | 说明 |
|---|---|---|
| 窗口最小化/关闭按钮、窗口拖拽区、`code==5` 消息 | `App.vue:103-143` | 桌面原生窗口行为已覆盖 |
| `GET /api/exit` | `FantController.cs:38` | 关闭窗口即退出 |
| `GET /api/test` | `TestController.cs:10` | 调试端点 |
| `EntityStackTrace` / `IntPtrReference` | `Nirvana.Public/Entities/Nirvana/EntityStackTrace.cs`、`Entities/IntPtrReference.cs` | 前者只服务 HTTP 异常过滤器（`WebApiExceptionFilter.cs:37`），后者全仓无引用 |

---

## 5. Top 10 最值得优先补的缺口

> 排序依据：**用户可感知价值 × 实现成本低**（价值高、成本低者在前）。

| 排名 | 缺口 | 为什么值得先做 | 源位置 | lanw 落点 | 工作量 |
|---|---|---|---|---|---|
| **1** | **账号「编辑」** | 端口/密码/类型填错就无法改正，只能删了重建；后端 `UpdateAccount` + `AccountRepository.Update` 都已就绪，只差一个弹窗 | `GameAccounts.vue:25,34-77` | `Views/AccountsPage.xaml.cs`（照抄 `AddAccount_Click:37-96`）+ `AccountsViewModel` 加命令 | 中 |
| **2** | **账号/代理/游戏实例的关闭确认框** | 三处误点即造成不可逆损失（账号被删、代理断线），实现是同一个 `ContentDialog` 模式，`PluginsPage.xaml.cs:56-82` 可直接复用 | `GameAccounts.vue`；`ProxyManager.vue:116-129`；`GameLaunchManager.vue:88-101` | 三个 `*.xaml.cs` | 小 |
| **3** | **`PluginMessage.Initialize()` 接入启动流程** | 一行调用解决「插件装了不生效」这一整类困惑；插件页开关才有真实语义 | `InitProgram.cs:58` | `App.xaml.cs:60` 之后 | 小 |
| **4** | **Java 运行时自动安装** | 没装 Java 的用户 100% 启动失败，且看不到可操作指引；这是「能不能玩」的分水岭 | `LaunchMessage.cs:133-180` | `Services/GameLaunchService.cs:104` 前后 | 中 |
| **5** | **服务器详情页一键启动（账号+角色+启动游戏/启动代理）** | 源项目的核心交互；现在用户必须在「启动管理」页手抄服务器 ID 与角色名，链路过长 | `ServerDetail.vue:8,44-100` | `ServerDetailPage.xaml` + `ServerDetailViewModel.cs` | 大 |
| **6** | **「添加名称」创建游戏角色** | 新服务器没有角色就无法启动；后端 `CreateCharacterAsync`/`CreateCharacterRental` 已就绪 | `ServerDetail.vue:65-68,383-402`；`GameRentalDetail.vue:238-249` | 随 #5 一起做 | 中 |
| **7** | **列表自动批量加载恢复** | 服务器/皮肤/租赁服三个主列表页都要手点几十次「加载更多」，感知极强；三处同一模式 | `Servers.vue:61-81`；`Skins.vue:135-177`；`GameRental.vue:58-78` | `ServersViewModel.cs:80`、`SkinsViewModel.cs:143`、`GameRentalViewModel.cs:80` | 中 |
| **8** | **租赁服详情页一键启动** | 与 #5 同源，租赁服玩家占相当比例；三线地址已移植，只差 Launch 弹窗 | `GameRentalDetail.vue:8,39-76` | `GameRentalDetailPage.xaml` + `GameRentalDetailViewModel.cs` | 大 |
| **9** | **设置项改动即保存** | 消除「改完忘点保存」的静默失败；改动极小（setter 内落盘） | `Settings.vue`（各 watch → API） | `ViewModels/SettingsViewModel.cs:95-104` 抽成 `Persist()` 并在 setter 调用 | 小 |
| **10** | **代理启动走服务器上下文（含核心模组安装）** | 现在代理必须手抄转发地址，且**不装核心模组会直接导致校验失败**（源注释已明说），用户表现为「代理连不上」 | `ProxiesMessage.cs:45-88` | `Services/ProxyService.cs:100` + 新增 `ProxiesMessage` | 大 |

> 若只做 5 项，建议 **#1 #2 #3 #4 #7**：全部为「小/中」成本，合计约 3–5 人日，可一次性消灭「账号改不了、误删没提醒、插件不生效、没 Java 玩不了、列表刷不动」这五类最高频投诉。

---

## 6. lanw 相对源项目的净新增（不计入覆盖率，供交叉核验）

| 新增能力 | 依据 |
|---|---|
| IRC 跨服聊天页（源项目无此页面，`Nirvana.Chat` 原为插件） | `Views/ChatPage.xaml`；`ViewModels/ChatViewModel.cs`；`Services/ChatService.cs:86-196`；`src/Core/Lanw.Chat/Message/ChatMessage.cs:24-251` |
| 启动管理页的完整启动参数区（版本/内存/JVM/服务器ID/角色名/进度/日志） | `Views/GameLaunchManagerPage.xaml:29-125`；`GameLaunchManagerViewModel.cs:142-350` |
| 代理页的拦截器配置表单（地址/端口/昵称/版本/模组/租赁开关）+ 一键启动拦截器 | `Views/ProxyManagerPage.xaml:25-125`；`ProxyManagerViewModel.cs:139-258` |
| 皮肤「上传本地 PNG 皮肤」 | `Views/SkinsPage.xaml:55-60`；`SkinsViewModel.cs:306 UploadLocalSkinAsync`；`Services/SkinService.cs:91`；`Services/SkinFilePicker.cs:48` |
| 版本页的更新检查（源页仅一句静态提示） | `ViewModels/VersionViewModel.cs:77 CheckUpdateAsync`；`Services/VersionService.cs:51-104` |
| 日志页自动刷新/清空开关 | `Views/LogsPage.xaml:39-43`；`LogsViewModel.cs:68,76` |
| 随机名生成器（源在 ServerDetail/GameRentalDetail 内联，lanw 提到主页） | `ViewModels/HomeViewModel.cs:128,136`；`src/Core/Lanw.Core/Utils/RandomNameUtil.cs` |

---

## 7. 审计局限

1. **未执行构建**（按任务要求）：所有结论均为静态阅读结论；`已移植` 表示「调用链存在且可读通」，不代表运行时行为已通过测试。
2. **`部分移植` 的边界依赖读码判断**：例如「富文本 vs 纯文本」「自动循环 vs 手动按钮」按源码语义判定，可能与实际产品意图不符。
3. **未覆盖的源代码面**：本次审计聚焦「前端页面 + Controller 端点 + Message 业务动作 + 启动初始化」。`Nirvana.Common` / `Nirvana.DevPlugin` / `Nirvana.Development` / `Nirvana.Cipher` / `Nirvana.Heypixel` 等**库级**等价性（逐方法比对）不在范围内，仅在其被 UI/端点直接引用时做了点状核对。
4. **标记为 `未确认` 的条目**：本报告未出现 `未确认` 条目——凡是无法用工具证实的，一律归入 `未移植` 并注明「无调用点 / 无同名文件」，未做推测性认定。
5. **并发开发导致的时间窗**：审计期间工作区有其他人在改源码（`src/App/Lanw.App/Views/HomePage.xaml*`、`ViewModels/HomeViewModel.cs`、`ViewModels/UserHomeViewModel.cs`、`Services/ThemeFilePicker.cs`、`src/Core/Lanw.Public/Message/ThemeMessage.cs`、`tests/Lanw.Core.Tests/ThemeMessageParseTests.cs` 在审计窗口内被写入）。报告已对主题相关条目复核更新；**其余条目的行号可能已被后续改动推移**，引用时请以当前文件为准。
