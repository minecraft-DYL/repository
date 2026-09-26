namespace OreUI.WinUI;

/// <summary>控件的交互状态，作为选择调色板的第二根轴（第一根轴是语义 status）。</summary>
public enum OreElementState
{
    /// <summary>常态。</summary>
    Normal = 0,

    /// <summary>指针悬停。</summary>
    PointerOver = 1,

    /// <summary>按下。</summary>
    Pressed = 2,

    /// <summary>禁用。</summary>
    Disabled = 3,
}

/// <summary>
/// 组件调色板表。所有色值逐条对应
/// <c>_source/src/components/controls/button/style.css</c> 与 <c>styles/oreui.css</c>，
/// 以及 <c>spec/oreui-tokens.json</c> 的语义分组。
/// </summary>
public static class OreUIPalettes
{
    // ---- 原子灰阶 ----
    internal const string Gray10 = "#F4F6F9";
    internal const string Gray20 = "#E6E8EB";
    internal const string Gray30 = "#D0D1D4";
    internal const string Gray40 = "#B1B2B5";
    internal const string Gray50 = "#8D8D90";
    internal const string Gray60 = "#58585A";
    internal const string Gray70 = "#48494A";
    internal const string Gray80 = "#313233";
    internal const string Gray90 = "#242425";
    internal const string Gray100 = "#1E1E1F";
    internal const string White = "#FFFFFF";
    internal const string Black = "#000000";

    // ---- 主色 / 状态色 ----
    internal const string Green10 = "#A0E081";
    internal const string Green30 = "#6CC349";
    internal const string Green50 = "#3C8527";
    internal const string Green60 = "#2A641C";
    internal const string Green70 = "#1D4D13";
    internal const string Red10 = "#F46D6D";
    internal const string Red50 = "#CA3636";
    internal const string Red60 = "#C02D2D";
    internal const string Red80 = "#AD1D1D";
    internal const string Blue10 = "#8CB3FF";
    internal const string Blue20 = "#2E6BE5";
    internal const string Yellow10 = "#FFE866";
    internal const string Orange20 = "#D3791F";
    internal const string DeepBlue50 = "#7345E5";
    internal const string DeepBlue100 = "#050029";

    // ---- 中性语义 ----
    internal const string NeutralBackground = Gray70;
    internal const string NeutralBackgroundHovered = Gray60;
    internal const string NeutralBackgroundPressed = Gray80;
    internal const string NeutralShadow = Gray80;
    internal const string NeutralText = White;
    internal const string NeutralBorder = Gray100;
    internal const string NeutralBorderFocused = White;
    internal const string NeutralBackgroundDisabled = Gray30;
    internal const string NeutralShadowDisabled = Gray40;
    internal const string NeutralTextDisabled = Gray70;
    internal const string NeutralBorderDisabled = "#8C8D90";
    internal const string Neutral60BorderTint = "#8C8D90";

    // ---- 高光 / 斜面 ----
    internal const string SpecularDefaultTopLeft = "#FFFFFF99";
    internal const string SpecularDefaultBottomRight = "#FFFFFF66";
    internal const string SpecularTopLeft = "#FFFFFF33";
    internal const string SpecularBottomRight = "#FFFFFF1A";
    internal const string White80 = "#FFFFFFCC";
    internal const string White60 = "#FFFFFF99";
    internal const string White50 = "#FFFFFF80";
    internal const string White40 = "#FFFFFF66";
    internal const string White30 = "#FFFFFF4D";
    internal const string White20 = "#FFFFFF33";
    internal const string White10 = "#FFFFFF1A";
    internal const string OverlayBackground = "#000000B3";

    /// <summary>按钮的立体调色板。</summary>
    public static OreBevelPalette Button(OreStatus status, OreButtonType type, OreElementState state)
    {
        if (type == OreButtonType.Sidebar)
        {
            return SidebarButton(state);
        }

        if (status == OreStatus.Disabled || state == OreElementState.Disabled)
        {
            return Disabled();
        }

        return status switch
        {
            OreStatus.Green => GreenButton(state),
            OreStatus.Red => RedButton(state),
            _ => NormalButton(state),
        };
    }

    private static OreBevelPalette NormalButton(OreElementState state) => state switch
    {
        OreElementState.PointerOver => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Gray40),
            Edge = OreUIColor.Brush(NeutralBorder),
            Shadow = OreUIColor.Brush(Gray60),
            ShadowThickness = 4,
            SpecularTopLeft = OreUIColor.Brush(White80),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(White60),
            SpecularBottomRightThickness = new(0, 0, 3, 7),
            Foreground = OreUIColor.Brush(Gray100),
        },
        OreElementState.Pressed => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Gray40),
            Edge = OreUIColor.Brush(NeutralBorder),
            SpecularTopLeft = OreUIColor.Brush(White80),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(White60),
            SpecularBottomRightThickness = new(0, 0, 3, 3),
            Foreground = OreUIColor.Brush(Gray100),
        },
        _ => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Gray30),
            Edge = OreUIColor.Brush(NeutralBorder),
            Shadow = OreUIColor.Brush(Gray60),
            ShadowThickness = 4,
            SpecularTopLeft = OreUIColor.Brush(SpecularDefaultTopLeft),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(SpecularDefaultBottomRight),
            SpecularBottomRightThickness = new(0, 0, 3, 7),
            Foreground = OreUIColor.Brush(Gray100),
        },
    };

    private static OreBevelPalette GreenButton(OreElementState state) => state switch
    {
        OreElementState.PointerOver => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Green60),
            Edge = OreUIColor.Brush(NeutralBorder),
            Shadow = OreUIColor.Brush(Green70),
            ShadowThickness = 4,
            SpecularTopLeft = OreUIColor.Brush(White40),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(White30),
            SpecularBottomRightThickness = new(0, 0, 3, 7),
            Foreground = OreUIColor.Brush(White),
        },
        OreElementState.Pressed => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Green70),
            Edge = OreUIColor.Brush(NeutralBorder),
            SpecularTopLeft = OreUIColor.Brush(White40),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(White30),
            SpecularBottomRightThickness = new(0, 0, 3, 3),
            Foreground = OreUIColor.Brush(White),
        },
        _ => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Green50),
            Edge = OreUIColor.Brush(NeutralBorder),
            Shadow = OreUIColor.Brush(Green70),
            ShadowThickness = 4,
            SpecularTopLeft = OreUIColor.Brush(SpecularTopLeft),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(SpecularBottomRight),
            SpecularBottomRightThickness = new(0, 0, 3, 7),
            Foreground = OreUIColor.Brush(White),
        },
    };

    private static OreBevelPalette RedButton(OreElementState state) => state switch
    {
        OreElementState.PointerOver => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Red60),
            Edge = OreUIColor.Brush(NeutralBorder),
            Shadow = OreUIColor.Brush(Red80),
            ShadowThickness = 4,
            SpecularTopLeft = OreUIColor.Brush(White50),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(White40),
            SpecularBottomRightThickness = new(0, 0, 3, 7),
            Foreground = OreUIColor.Brush(White),
        },
        OreElementState.Pressed => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Red80),
            Edge = OreUIColor.Brush(NeutralBorder),
            SpecularTopLeft = OreUIColor.Brush(White50),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(White40),
            SpecularBottomRightThickness = new(0, 0, 3, 3),
            Foreground = OreUIColor.Brush(White),
        },
        _ => new OreBevelPalette
        {
            Fill = OreUIColor.Brush(Red50),
            Edge = OreUIColor.Brush(NeutralBorder),
            Shadow = OreUIColor.Brush(Red80),
            ShadowThickness = 4,
            SpecularTopLeft = OreUIColor.Brush(SpecularTopLeft),
            SpecularTopLeftThickness = new(3, 3, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(SpecularBottomRight),
            SpecularBottomRightThickness = new(0, 0, 3, 7),
            Foreground = OreUIColor.Brush(White),
        },
    };

    private static OreBevelPalette Disabled() => new()
    {
        Fill = OreUIColor.Brush(NeutralBackgroundDisabled),
        Edge = OreUIColor.Brush(NeutralBorderDisabled),
        Shadow = OreUIColor.Brush(NeutralShadowDisabled),
        ShadowThickness = 4,
        Foreground = OreUIColor.Brush(NeutralTextDisabled),
    };

    private static OreBevelPalette SidebarButton(OreElementState state)
    {
        if (state is OreElementState.PointerOver or OreElementState.Pressed)
        {
            return new OreBevelPalette
            {
                Fill = OreUIColor.Brush(NeutralBackgroundHovered),
                Edge = OreUIColor.Brush(NeutralBorderFocused),
                Foreground = OreUIColor.Brush(NeutralText),
            };
        }

        return new OreBevelPalette
        {
            Fill = OreUIColor.Brush(NeutralBackground),
            Edge = OreUIColor.Brush(NeutralShadow),
            SpecularTopLeft = OreUIColor.Brush(Neutral60BorderTint),
            SpecularTopLeftThickness = new(2, 2, 0, 0),
            SpecularBottomRight = OreUIColor.Brush(NeutralBackgroundPressed),
            SpecularBottomRightThickness = new(0, 0, 2, 2),
            Foreground = OreUIColor.Brush(NeutralText),
        };
    }

    /// <summary>复选框的立体调色板（20x20，含勾选态）。</summary>
    public static OreBevelPalette CheckBox(bool isChecked, bool isEnabled, OreElementState state)
    {
        var background = (isChecked, isEnabled, state) switch
        {
            (_, false, _) => NeutralBackgroundDisabled,
            (true, true, OreElementState.PointerOver) => Green60,
            (true, true, OreElementState.Pressed) => Green70,
            (true, true, _) => Green50,
            (false, true, OreElementState.PointerOver) => Gray40,
            (false, true, OreElementState.Pressed) => Gray60,
            _ => NeutralBorderDisabled,
        };

        var edge = !isEnabled
            ? NeutralBorderDisabled
            : isChecked ? NeutralBorder : NeutralBorder;

        return new OreBevelPalette
        {
            Fill = OreUIColor.Brush(background),
            Edge = OreUIColor.Brush(edge),
            SpecularTopLeft = isEnabled ? OreUIColor.Brush(White20) : null,
            SpecularTopLeftThickness = new(2, 2, 0, 0),
            SpecularBottomRight = isEnabled ? OreUIColor.Brush(White10) : null,
            SpecularBottomRightThickness = new(0, 0, 2, 2),
            Foreground = OreUIColor.Brush(isEnabled ? NeutralText : NeutralTextDisabled),
        };
    }

    /// <summary>开关（Switch）轨道与滑块的配色。返回 (轨道左半色, 轨道右半色, 滑块面, 滑块边)。</summary>
    public static (string TrackOn, string TrackOff, string KnobFill, string KnobEdge) Switch(bool isOn, bool isEnabled)
    {
        if (!isEnabled)
        {
            return (NeutralBackgroundDisabled, "#CFD0D4", "#CFD0D4", NeutralBorderDisabled);
        }

        return (Green50, NeutralBorderDisabled, Gray30, NeutralBorder);
    }
}
