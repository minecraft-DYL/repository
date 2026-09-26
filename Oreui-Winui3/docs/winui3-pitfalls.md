# WinUI 3 移植踩坑记录

本文件收录 `OreUI.WinUI` 移植过程中**实际踩到并已修复**的坑。每条都标注了症状、根因、修法，
以及「怎么确认修好了」。踩坑时最贵的不是修，是**定位**——所以症状描述尽量写成可搜索的样子。

---

## 1. 依赖动画（dependent animation）被静默丢弃 ⭐ 最坑的一条

**症状**：动画「不生效」而不是「报错」。表现五花八门，很难联想到同一个原因：

- 开关切换后滑块停在**上一次的位置**（禁用开关的滑块位置和启用开关对不上）；
- `OrePopHost.Show()` 调了没反应，气泡永久停在 `Opacity=0`（「点了弹出气泡没反应」）；
- `OreShowBlock` 的高光**压根不扫过去**（不只是位置不对）；
- 侧边栏展开/收起变成硬切，没有滑动过渡。

**根因**：`TranslateTransform.X/Y`、`Width`、`Height` 属于 **dependent animation** 属性。
`DoubleAnimation` 作用在这些属性上时，**必须**显式设 `EnableDependentAnimation = true`，
否则 WinUI 会**静默丢弃整条动画**——不抛异常、不写日志、`Storyboard.Completed` 照常触发。
`Opacity` 是 `UIElement` 自身的属性，属于 independent animation，不需要这个开关
（所以「淡入生效、滑入没生效」是很典型的组合症状）。

**修法**：全库所有指向 transform 属性的 `DoubleAnimation` 都补上：

```csharp
var animation = new DoubleAnimation
{
    To = target,
    Duration = new Duration(TimeSpan.FromMilliseconds(125)),
    EnableDependentAnimation = true,   // ← 少了这句整条动画消失
    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 },
};
```

本仓库涉及 4 处：`OreToggleSwitch.MoveKnob`、`OrePop.Animate`（slide 那条）、
`OreBanner.BuildFlashAnimation`、`OreModal.UpdatePanelVisual`。

**防回归**：`grep -n "SetTargetProperty.*\"[XYWH]\"" dotnet/src/OreUI.WinUI/Controls/*.cs`，
每一处都要能看到同一作用域里的 `EnableDependentAnimation = true`。

**确认修好的证据**：点击「弹出气泡」后截图，底部区域统计到
`#1F1F1F`(气泡底色) 4885px + `#6CC349`(success 文字色) 207px；修之前这两个值都是 0。

---

## 2. `Application.Resources` 少了 `XamlControlsResources` → 点加载遮罩直接崩

**症状**：点「显示加载遮罩」进程 fail-fast。事件日志里只有：

```
Faulting module name: Microsoft.UI.Xaml.dll, version 3.1.8.0
Exception code: 0xc000027b          ← STOWED_EXCEPTION
```

`Application.UnhandledException` **收不到**，`gallery-errors.log` 一直是空的。

**根因**：`App.xaml` 的 `Application.Resources` 里漏了框架默认样式表
`XamlControlsResources`。少了它，任何 WinUI 内置控件都拿不到默认样式；
`OreLoadingMask` 里的 `ProgressRing` 第一次可见时会去查框架资源并抛
`Cannot find a Resource with the Name/Key TabViewButtonBackground`，
这个异常被 WinUI 收成 stowed exception 后直接 fail-fast。

> 这个坑曾经被「修反」过：有人看到相对 `ResourceDictionary.Source="X.xaml"` 抛
> 「String 无法赋给 Uri」，就把 `XamlControlsResources` 删掉了。
> 那个报错来自**相对 URI 的合并字典**，跟 `XamlControlsResources` 无关。
> **不要删它。**

**修法**：

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

**怎么抓到真凶**：stowed exception 抓不到，就用 `AppDomain.CurrentDomain.FirstChanceException`
在异常**第一次抛出**的瞬间落盘（按 `类型|HResult|首帧` 去重，否则会被运行时内部正常吞掉的异常刷屏）。
这是本仓库唯一能拿到 `0xc000027b` 真实堆栈的手段。

---

## 3. WinUI 默认 `Button` 模板会覆盖你设的 `FontFamily` → 字形豆腐块

**症状**：弹窗右上角关闭键的 `&#xE711;` 显示成豆腐块。即使按钮上明明写了
`FontFamily="Segoe Fluent Icons"` 也没用。

**根因**：WinUI 默认 `Button` 模板里的 `ContentPresenter` 会把 `FontFamily` 固定成
内容字体主题资源，**覆盖**继承来的值。而且 OreUI 自己的字体族
（Minecraft Ten / Seven / Five、Noto Sans）**不含图标字形**，只会得到豆腐块。

**修法**：按上游来——`.modal_close_btn_img` 本来就是一张 `<img>`，所以关闭键直接改用位图
（`cross_white.png`，原图 52×52，显示 20×20）。确实需要字体图标时才用
`FontFamily="Segoe Fluent Icons"`，且要意识到它可能被模板覆盖。

---

## 4. 系统焦点视觉在加了 `XamlControlsResources` 之后才开始「冒出来」

**症状**：弹窗打开时上面多出一圈高亮（用户描述为「黄色高亮」）。

**根因**：三个样式文件里写了 `UseSystemFocusVisuals="True"`。**在合并
`XamlControlsResources` 之前**，WinUI 内置控件没有默认样式，焦点视觉根本不工作；
合并之后它开始生效，弹窗打开时焦点落到主按钮上就画出来了。也就是说：
**第 2 条的修复会「激活」这一类之前被掩盖的视觉问题**，两者要一起看。

**修法**：OreUI 表达聚焦用的是 `neutral-border-focused`（纯白描边），不是系统焦点矩形：

```xml
<Setter Property="UseSystemFocusVisuals" Value="False" />
<Setter Property="FocusVisualPrimaryThickness" Value="0" />
<Setter Property="FocusVisualSecondaryThickness" Value="0" />
```

**未验证到**：修复前的构建已被清理，拿不到「有黄色」的对照截图。
当前构建实测**全图 yellowish / broad-yellow 像素均为 0**，但没有 before 对照，
所以**不能断定**黄框就是这个系统焦点视觉——只能说代码路径吻合且当前已无黄色像素。

---

## 5. XAML 注释里不能出现 `--`

**症状**：`error WMC9997: XML 注释中不能包含"--"`。

**根因**：写注释时顺手抄了 CSS 变量名 `--neutral-border-focused`。

**修法**：注释里写 `neutral-border-focused`，别带 `--` 前缀。

---

## 6. 像素量测：全图 `GetPixel` 在 PowerShell 里会慢到跑不完

**症状**：`measure_controls.ps1` 跑十几分钟不出结果。

**根因**：1280×900 = 115 万次 `Bitmap.GetPixel()` 调用 + PowerShell 的解释开销。

**修法**：把像素分析整体挪进 C#，用 `LockBits` + `Marshal.Copy` 一次性取原始字节，
连通域标记也在 C# 里做。同样的分析从「分钟级」降到「毫秒级」。

**注意**：`Add-Type` 需要 `-ReferencedAssemblies System.Drawing`，C# 里还要
`using System.Linq;`，否则会报 `Bitmap`/`Graphics` 找不到。

---

## 7. 其他工程性坑（速查）

| 坑 | 症状 | 修法 |
|---|---|---|
| 库设了 `GenerateLibraryLayout` | 消费方 `XamlTypeInfo.g.cs` 里没有库的 XAML 类型，报 `type 'OreButton' was not found ... unknown namespace` | 这是 C++/WinRT 布局模式，C# 类库**不要**设 |
| `dotnet build -o <dir>` | `App.xbf`/`MainWindow.xbf` 不复制，伪装成 `XamlParseException` | 永远不要用 `-o` |
| `<Project TreatAsLocalProperty="Platform">` 写成 `<PropertyGroup ...>` | `error MSB4066: 无法识别元素 <PropertyGroup> 中的特性` | 它是 **`<Project>` 的属性** |
| `.slnx` + 自定义配置名 | `error MSB4126: 指定的解决方案配置"X\|Any CPU"无效` | 自定义配置只存在于**项目**配置里，直接 build 项目 |
| 崩溃的 WinUI 进程变僵尸 | 锁住输出目录 DLL，`MSB3027/MSB3021` | 每轮换新配置名（RelL、RelM…） |
| 合并字典不共享资源查找域 | `Cannot find a Resource with the Name/Key ...` | 生成**单一自包含**的 `Generic.xaml` |
| 库 Content 的 URI 前缀 | 字体/图片静默取不到 | 库 Content 落到 `<app>\<AssemblyName>\...`，URI 必须写 `ms-appx:///OreUI.WinUI/Assets/...` |
| `FontFamily` 的 `#` 片段 | 字体静默回退 | 必须是字体**内部**族名（OpenType nameID 1），用 `tools/font_info.py` 读 |
| 非 ASCII 的 .cs/.xaml | 编译/解析报错 | 需要 UTF-8 BOM，`tools/ensure_bom.py` |
| 用 `CopyFromScreen` 截图 | 抓到的是叠加后的桌面（实测 40% 是别的窗口） | 用 `PrintWindow(hwnd, dc, PW_RENDERFULLCONTENT=2)` |
| 遮罩/弹窗点击崩溃 | 同步折叠「正在派发 Tapped 的那个元素」 | 折叠动作 `DispatcherQueue.TryEnqueue` 推迟一帧 |
| CS0108 遮蔽 | `OreSidebarItem.Tag` 遮住 `FrameworkElement.Tag`，形成两块互不相通的存储 | 不要重新声明基类已有的成员，直接用继承来的 |
