# OreUI → WinUI 3 (.NET) 移植

把 [Spectrollay-OreUI/OreUI](https://github.com/Spectrollay-OreUI/OreUI) 这套
「我的世界 OreUI」设计语言移植到 **.NET / WinUI 3（Windows App SDK）**。

目标不是把 HTML 塞进 WebView，而是**用 WinUI 3 的原生控件体系重新实现一遍**：
接口按 WinUI 的习惯来（`Content` / `Click` / `IsChecked` / `IsEnabled` / 依赖属性 /
`ThemeResource`），皮肤是 OreUI 的（3D 斜切描边、`#1E1E1F` 底、NotoSans Bold 正文、
Minecraft Ten 标题）。

> **Python/Tk 版本已按用户要求取消**，本仓库只有 .NET 一份。

---

## 现状

已验证可运行：`dotnet/OreUI.WinUI.slnx` 干净构建 → Gallery 启动无异常 →
真实窗口渲染与 OreUI 调色板逐像素吻合。

```
┌──────────────────────────────────────────────────────────────┐
│ 25 个控件   ·  526 条设计令牌  ·  ~2 750 行 C#  ·  7 个 XAML 片段 │
└──────────────────────────────────────────────────────────────┘
```

| 指标 | 值 | 怎么验的 |
| --- | --- | --- |
| 控件数 | 25 | `Controls/*.cs` 里的 `public class` |
| 令牌数 | 526 条 `x:Key` | `tools/gen_tokens.py --check` |
| 渲染正确性 | `#1E1E1F` 占 61.6%、`#313233` 23.7%、`#D0D1D4` 4.0%、`#3C8527` 0.72% | `tools/analyze_capture.ps1` 统计截图像素 |
| 画面干净度 | 1280×900 全图只有 **257 种颜色** | 同上（说明没有渐变糊边、没有桌面串色） |
| 字体生效 | 改字体路径前后文本区域差异 **12.5%**，路径写错则静默回退 | 截图逐像素对比 |

---

## 快速开始

```powershell
# 依赖：.NET SDK 10、Windows App SDK 1.8
cd dotnet

# 构建整个解决方案（确认能过）
dotnet build OreUI.WinUI.slnx -c Release

# 单独构建示例应用（自定义配置名时走项目，不要走 .slnx）
dotnet build samples\OreUI.Gallery\OreUI.Gallery.csproj -c Release -p:Platform=x64

# 运行（非打包、自包含，不需要预装 WindowsAppRuntime）
.\samples\OreUI.Gallery\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\OreUI.Gallery.exe
```

在自己的项目里用：

```xml
<ProjectReference Include="..\OreUI.WinUI\OreUI.WinUI.csproj" />
```

```xml
xmlns:oreui="using:OreUI.WinUI"

<oreui:OrePageTitle Content="组件总览" />
<oreui:OreButton Content="开始游戏" Status="Green" Size="Large" Click="OnClick" />
<oreui:OreCheckBox Content="启用" IsChecked="True" />
<oreui:OreSlider Minimum="0" Maximum="100" Value="45" Segments="5" />
```

宿主应用**不需要自己分发字体**：库把 OreUI 原版字体作为 `Content` 带出去。

---

## 仓库结构

```
_source/                     上游 OreUI 的浅克隆（MIT），只读参考，勿改
spec/oreui-tokens.json       设计令牌唯一事实来源（颜色/特效/排版/组件尺寸）
xaml-src/                    XAML 片段源文件（人写 + 生成）
  00-tokens.xaml             ← tools/gen_tokens.py 生成，勿手改
  10-button.xaml 20-controls.xaml 30-surfaces.xaml
  40-overlays.xaml 50-inputs.xaml 60-sidebar.xaml
tools/
  gen_tokens.py              spec → xaml-src/00-tokens.xaml
  gen_theme.py               xaml-src/* → Themes/Generic.xaml（合并成单文件）
  font_info.py               读字体内部 family 名（WinUI 的 #Family 必须是它）
  ensure_bom.py              给含中文的 .cs/.xaml 补 UTF-8 BOM
  capture_window.ps1         截图（PrintWindow，抗窗口遮挡）
  analyze_capture.ps1        截图像素 → 调色板命中率，做回归断言
dotnet/
  Directory.Build.props      锁 x64（见下文「坑 4」）
  OreUI.WinUI.slnx
  src/OreUI.WinUI/           控件库
    Controls/*.cs            25 个控件 + 斜边渲染器 + 枚举 + 调色板
    Themes/Generic.xaml      ← tools/gen_theme.py 生成，唯一的默认样式表
    Assets/OreUI/Fonts/      上游原版字体（10 个文件）
  samples/OreUI.Gallery/     组件总览示例（非打包 WinUI 3）
```

### 生成流水线

```
spec/oreui-tokens.json ──gen_tokens.py──▶ xaml-src/00-tokens.xaml ─┐
xaml-src/10..60-*.xaml ────────────────────────────────────────────┴─gen_theme.py─▶ Themes/Generic.xaml
```

两个脚本都支持 `--check`（只校验磁盘产物是否与源一致，CI 用）：

```powershell
python tools\gen_tokens.py --check
python tools\gen_theme.py  --check
```

**为什么令牌只维护一份**：上游是 CSS 变量，这里如果手抄一遍 `#RRGGBBAA`，
两边一定会漂移。所以颜色只在 `spec/oreui-tokens.json` 里写，.NET 侧的
`Color` / `SolidColorBrush` / 字号 / 尺寸全部由脚本产出。注意 CSS 是 `#RRGGBBAA`，
XAML 是 `#AARRGGBB`，脚本负责换位。

---

## 架构决定

### 1. 3D 斜边怎么来的（最关键的一条）

OreUI 的「伪 3D」在浏览器里靠 CSS 内阴影 `box-shadow: inset …` 实现。
桌面端**没有 inset shadow**，所以把每层内阴影拆成一条显式绘制的边：

| CSS | WinUI 实现 |
| --- | --- |
| `inset 3px 3px <c>` | 左边 3px + 上边 3px 两条带 → `BorderThickness="3,3,0,0"` |
| `inset -3px -7px <c>` | 右边 3px + 下边 7px → `BorderThickness="0,0,3,7"` |
| `inset 0 -4px <c>` | 底部 4px 实心条 |
| `inset 0 4px <c>` | 顶部 4px 实心条 |

两个必须记住的细节：

- **内阴影裁切到 padding box**，所以各层要放在 `Grid` 里并让
  `Margin="{TemplateBinding BorderThickness}"`。
- **CSS 的绘制顺序是「先写的阴影在最上层」**，所以 XAML 里底部阴影条要
  **写在最后**，否则叠出来的明暗关系是反的。

### 2. 调色板算在 C# 里，不用 VisualState

OreUI 的状态轴是 `status`(Normal/Green/Red/Disabled) × 交互
(Normal/Hover/Active)，逐个写成 VisualState 要 ~14 组几乎相同的定义。
这里改成 `OreUIPalettes.Button(status, type, state)` 一个纯函数，
由 `OreControlBase` 在指针进出 / `IsEnabled` 变化时算好颜色，
通过 `OreBevelPresenter` 塞进模板里的具名部件
（`OreFillBorder` / `OreSpecularTopLeft` / `OreSpecularBottomRight` / `OreBottomShadow`）。

好处：颜色逻辑**可单测**、模板只有一份、状态机只有一处。

### 3. 继承真实 WinUI 控件 vs 自建 Control

| 做法 | 用在哪 | 为什么 |
| --- | --- | --- |
| 继承真实控件<br/>`OreButton : Button`、`OreCheckBox : CheckBox`、`OreTextBox : TextBox` | 模板契约宽松的 | 白拿键盘/无障碍/输入法/`Click`/`IsChecked` 全套行为 |
| 自建 `OreControlBase : Control` | 需要完全掌控模板的 | 指针/启用状态机 + 斜边渲染器复用 |

枚举字符串值和上游保持一致（`Status="Green"`、`BannerType="Important"`…），
方便和 CSS 对照。

### 4. 每个移植都服从**宿主框架**的习惯

设计原则是「用宿主框架的写法，穿 OreUI 的皮」：

| OreUI (Web) | WinUI 3 |
| --- | --- |
| `<oreui-button status="green">` | `<oreui:OreButton Status="Green">` |
| 子节点文字 | `Content="…"` |
| `addEventListener('click')` | `Click="…"` |
| `class="checked"` | `IsChecked="True"` |
| CSS 变量 | `ThemeResource` / 依赖属性 |
| `<slot>` | `ContentPresenter` |

---

## 控件清单（25 个）

| 分组 | 控件 | 说明 |
| --- | --- | --- |
| 按钮 | `OreButton` | 尺寸 XS/S/M/L、状态 Normal/Green/Red/Disabled、图标左右、`Countdown` 倒计时、`Tip` 气泡、`Type=Sidebar` 变体 |
| 按钮 | `OreButtonGroup` | 水平拼装一组按钮 |
| 选择 | `OreCheckBox` | 四态（未选/选中/禁用/禁用选中） |
| 选择 | `OreToggleSwitch` | 滑块 ＋ 轨道双层斜边 |
| 选择 | `OreSlider` | 支持 `Segments`/`StepFrequency`、`IsEnabled=False` |
| 输入 | `OreTextBox` | 占位符/只读/禁用/多行换行 |
| 输入 | `OreTextField` | 输入框 ＋ 标签容器 |
| 展示 | `OreTag` | 五种 `Accent`：Neutral/Green/Blue/Yellow/Red |
| 展示 | `OreBadge` | 同上五色圆点 |
| 展示 | `OreDivider` / `OreLine` / `OreDividerBox` / `OreVerticalDivider` | 分隔线系列 |
| 展示 | `OreBanner` | Neutral/Information/Important |
| 展示 | `OreShowBlock` | 带图标与描述的链接块，悬停有高光扫过 |
| 布局 | `OreMainBlock` / `OreMainBlockFrame` / `OreMainBlockSpacing` / `OreMainDisplay` / `OrePageTitle` | 主区块骨架 |
| 容器 | `OreSidebar` / `OreSidebarItem` | 左侧导航，宽窄模式 |
| 覆盖层 | `OreModal` | 带主/关闭按钮的模态框 |
| 覆盖层 | `OreLoadingMask` | 遮罩 ＋ 转圈 ＋ 错误/加载文案 |
| 覆盖层 | `OrePopHost` | 右下角 pop / toast 堆叠，超量自动裁剪 |

未移植：`OreComboBox`（需要 `Popup` 单独设计）、音效（上游 `click`/`button` 两种
提示音）。

---

## 踩过的坑（都已在代码里固定住，别再踩回去）

详细排查过程见 [`docs/winui3-pitfalls.md`](docs/winui3-pitfalls.md)。摘要：

1. **C# WinUI 库不要设 `GenerateLibraryLayout`。** 那是 C++/WinRT 的布局模式，
   会把 XBF 撒成散文件而不是嵌进程序集，导致引用方 App 的 `MarkupCompilePass2`
   扫不到本库的 XAML 类型，运行时抛
   `type 'OreButton' was not found because 'clr-namespace:OreUI.WinUI;assembly=OreUI.WinUI' is an unknown namespace`。
   验证方法：数一数 App 的 `obj\...\XamlTypeInfo.g.cs` 里有没有本库的类型。

2. **`MergedDictionaries` 之间不共享资源查找作用域。** 令牌写在 A 字典、
   样式写在 B 字典时，B 里的 `{ThemeResource X}` 找不到 A 的 X——
   运行时报 `Cannot find a Resource with the Name/Key OreUIFontSizePageTitle`。
   所以 `tools/gen_theme.py` 把 7 个片段**合并成单个自包含的 `Generic.xaml`**。

3. **库字典里不要用相对 `Source="X.xaml"`。** XBF 化之后运行时会报
   `Failed to assign to property 'ResourceDictionary.Source' because the type 'Windows.Foundation.String' cannot be assigned to the type 'Windows.Foundation.Uri'`。
   用绝对 `ms-appx:///…`，最好是干脆不嵌套（见第 2 条）。

4. **解决方案构建里 `Platform` 是全局属性。** `.slnx` 未声明平台时会传
   `Platform=AnyCPU`，而全局属性**压过任何 props 赋值**——所以
   `Directory.Build.props` 里那句 `<Platform>x64</Platform>` 会被静默忽略，
   库落到 `bin\Release`、应用落到 `bin\x64\Release`，两边 PRI 与 Content 拷贝对不上，
   表现为启动即 `XamlParseException`。修法是 `<Project TreatAsLocalProperty="Platform">`。

5. **`FontFamily` 的 `#` 后面必须是字体的内部 family 名**，不是 CSS `@font-face`
   起的逻辑名：`Minecraft-Ten.otf` 内部叫 `Minecraft Ten v2`，四个 NotoSans 文件
   内部都叫 `Noto Sans`。写错不报错，只是静默回退到系统字体。
   用 `python tools/font_info.py --markdown` 核对。

6. **库的 `Content` 会被放到以程序集名命名的子目录**（`<app>\OreUI.WinUI\…`），
   所以字体引用要带前缀：`ms-appx:///OreUI.WinUI/Assets/OreUI/Fonts/…`。

7. **非打包 WinUI 3 不要在 `App.xaml` 合并 `XamlControlsResources`**，
   会破坏资源查找并抛 `String 无法赋给 Uri`。

8. **`XamlReader.Load` 解析不了 WinRT 组件程序集的命名空间**，
   所以「切 XAML 片段试探」这条路是死的——只能改文件、重启应用来二分。

9. **截图要用 `PrintWindow(PW_RENDERFULLCONTENT)`**。`CopyFromScreen` 抓的是
   合成后的桌面，机器上随便一个窗口压上来，你截到的就是别人的界面
   （实测有一张截图 40% 是另一个应用）。`tools/capture_window.ps1` 默认走 PrintWindow。

10. **崩过的 WinUI 进程会变成杀不掉的僵尸**，锁住输出目录里的 DLL，
    后续构建报 `MSB3027 文件被 … 锁定`。换一个配置名构建即可绕开。

---

## 保真度取舍（说清楚，不装作 100% 一致）

| 项 | 上游 | 本移植 | 影响 |
| --- | --- | --- | --- |
| 3D 斜边 | CSS `inset box-shadow` | 拆成显式边层 | 视觉等价；极端 DPI 下 1px 位置可能差一像素 |
| 状态样式 | CSS 选择器组合 | C# 计算调色板 | 等价，且颜色逻辑可单测 |
| 图标 | 内联 SVG data URI | 位图资源（`cross_white.png`/`check_white.png`/箭头等）优先，其余留 Segoe Fluent Icons 字形 | 位图部分与上游像素一致；字形部分字形不同，接口保留 `IconGlyph`/`IconSource` 两种 |
| 字体回退 | CSS `font-family: "X", sans-serif` 列表 | WinUI `FontFamily` 不支持逗号回退列表 | 字体缺失时只能整体回退，无法逐级回退 |
| 动画 | CSS transition | `Storyboard` + `DoubleAnimation` | 曲线已按上游时长/缓动复刻；**必须**给 transform 动画开 `EnableDependentAnimation`（见 `docs/winui3-pitfalls.md` 第 1 条） |
| 焦点态 | `--neutral-border-focused` 白色描边 | 关掉系统焦点视觉（`UseSystemFocusVisuals=False`），白描边尚未接进 VSM | 键盘用户暂时看不到焦点提示，属无障碍欠缺 |
| 加载指示器 | `Loading_white.gif`（动画 GIF，60×60） | 同一个 GIF，交给 WinUI `Image` 播放 | 一致（**不是** `ProgressRing`） |
| 遮罩滚动条 | `scrollbar-width: none` / `overflow: hidden` | `ScrollBarVisibility="Hidden"` | 一致 |
| 气泡宽度 | `width: min(90%, 500px)` | 只有 `MaxWidth=500` | 窄窗口下气泡不会自动收到 90% |
| 弹窗内容高度 | `max-height: min(450px, 90vh - 150px)` | 固定 `MaxHeight=450` | 矮窗口下弹窗可能超出可用高度 |
| 错误文案描边 | `-webkit-text-stroke: 0.05em #FFFFFF` | 未实现 | 错误态只有红字加粗，没有白色描边 |
| 音效 | 点击/按钮提示音 | 未实现 | — |
| `OreComboBox` | 有 | 未移植 | — |

---

## 许可

- 上游 OreUI 代码为 **MIT**，`_source/` 原样保留作为对照，MIT 声明一并保留。
- `Assets/OreUI/Fonts/` 下的 Minecraft Ten / Seven / Five 字体版权归 Mojang，
  仅因上游仓库随附而一并分发；NotoSans 为 SIL OFL。
  若你要发布产品，请自行确认这些字体在你的场景下的可用性。
