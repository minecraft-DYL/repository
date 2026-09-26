using Microsoft.UI.Xaml;

namespace OreUI.WinUI;

/// <summary>
/// OreUI 控件的语义状态，对应 Web 端 <c>status</c> 属性的
/// <c>normal / green / red / disabled</c> 取值。
/// </summary>
public enum OreStatus
{
    /// <summary>默认中性（浅灰）配色。</summary>
    Normal = 0,

    /// <summary>主色 / 确认操作（绿色）。</summary>
    Green = 1,

    /// <summary>危险 / 破坏性操作（红色）。</summary>
    Red = 2,

    /// <summary>禁用态。</summary>
    Disabled = 3,
}

/// <summary>按钮尺寸，对应 Web 端 <c>size</c> 属性。</summary>
public enum OreButtonSize
{
    /// <summary>100px。</summary>
    ExtraSmall = 0,

    /// <summary>130px。</summary>
    Small = 1,

    /// <summary>200px（默认）。</summary>
    Middle = 2,

    /// <summary>272px。</summary>
    Large = 3,
}

/// <summary>按钮外观变体，对应 Web 端 <c>type</c> 属性。</summary>
public enum OreButtonType
{
    /// <summary>标准 OreUI 立体按钮。</summary>
    Default = 0,

    /// <summary>侧边栏扁平按钮（140px，无立体高光）。</summary>
    Sidebar = 1,
}

/// <summary>按钮内图标相对文字的方位。</summary>
public enum OreIconPosition
{
    /// <summary>图标在文字左侧（默认）。</summary>
    Left = 0,

    /// <summary>图标在文字右侧。</summary>
    Right = 1,
}

/// <summary>消息条（Banner）的语义类型。</summary>
public enum OreBannerType
{
    /// <summary>中性深色底。</summary>
    Neutral = 0,

    /// <summary>信息蓝底。</summary>
    Information = 1,

    /// <summary>重要黄底黑字。</summary>
    Important = 2,
}

/// <summary>气泡提示（Pop）的语义类型。</summary>
public enum OrePopStatus
{
    /// <summary>无状态着色（白字）。</summary>
    None = 0,

    /// <summary>成功（绿字）。</summary>
    Success = 1,

    /// <summary>进行中（黄字）。</summary>
    Process = 2,

    /// <summary>错误（红字）。</summary>
    Error = 3,

    /// <summary>VIP（金黄字）。</summary>
    Vip = 4,

    /// <summary>调试文本（黄底黑字）。</summary>
    DebugText = 5,
}

/// <summary>徽标（Badge）与标签（Tag）的语义配色。</summary>
public enum OreAccent
{
    /// <summary>中性（Tag 默认黑底白字 / Badge 白点）。</summary>
    Neutral = 0,

    /// <summary>绿色。</summary>
    Green = 1,

    /// <summary>蓝色。</summary>
    Blue = 2,

    /// <summary>黄色。</summary>
    Yellow = 3,

    /// <summary>红色。</summary>
    Red = 4,
}

/// <summary>语义化配色键，与 <c>spec/oreui-tokens.json</c> 的 semantic 分组一一对应。</summary>
public enum OreColorRole
{
    /// <summary>中性灰。</summary>
    Neutral = 0,

    /// <summary>主色（绿）。</summary>
    Primary = 1,

    /// <summary>次要色（浅灰）。</summary>
    Secondary = 2,

    /// <summary>成功。</summary>
    Success = 3,

    /// <summary>提示（蓝）。</summary>
    Informative = 4,

    /// <summary>危险（红）。</summary>
    Destructive = 5,

    /// <summary>警告（橙）。</summary>
    Notice = 6,

    /// <summary>Realms 业务线主色（紫）。</summary>
    RealmsPrimary = 7,
}
