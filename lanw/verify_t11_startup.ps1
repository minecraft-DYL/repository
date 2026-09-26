# t11 运行时验证：启动 Lanw.App.exe，确认无需任何点击即显示登录页（LoginPage）。
# 证据链：
#   1) 进程存活且未崩溃（MainWindowHandle 非零）
#   2) UIA 树中存在 LoginPage 独有元素（"登录账号" 标题文本 / "登录类型" Header / "登录" 按钮）
#   3) 全窗口截图保存 PNG 供人工复核
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_t11_startup.png"
)

$ErrorActionPreference = 'Stop'

# 清理可能残留的实例
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

Write-Host "[1] 启动: $ExePath"
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 10   # 等待窗口初始化 + XAML Loaded + 首帧渲染

$proc.Refresh()
$alive = -not $proc.HasExited
Write-Host "[2] 进程存活: $alive  MainWindowHandle: $($proc.MainWindowHandle)"

if (-not $alive -or $proc.MainWindowHandle -eq 0) {
    Write-Host "FAIL: 进程未存活或无主窗口"
    exit 2
}

Add-Type -AssemblyName UIAutomationClient
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

# 在整个 UIA 树中找 LoginPage 独有的元素名
$needle = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, "登录账号")
$loginTitle = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $needle)

$needle2 = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, "使用 Cookie 或密码登录你的账号")
$loginSub = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $needle2)

$needle3 = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, "登录类型")
$comboHeader = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $needle3)

Write-Host "[3] UIA 找到 '登录账号' 标题: $($null -ne $loginTitle)"
Write-Host "[3] UIA 找到 副标题文本: $($null -ne $loginSub)"
Write-Host "[3] UIA 找到 '登录类型' ComboBox Header: $($null -ne $comboHeader)"

# 全窗口截图（前台窗口）
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Rect {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
}
"@
[Win32Rect]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 800
$rect = New-Object Win32Rect+RECT
[Win32Rect]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
Write-Host "[4] 窗口区域: ${w}x${h} @ ($($rect.Left),$($rect.Top))"
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "[5] 截图已保存: $ShotPath"

$proc.Refresh()
$stillAlive = -not $proc.HasExited
Write-Host "[6] 截图后进程仍存活: $stillAlive"

# 结束验证进程
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

$ok = ($alive -and $stillAlive -and ($null -ne $loginTitle) -and (($null -ne $loginSub) -or ($null -ne $comboHeader)))
Write-Host ""
if ($ok) {
    Write-Host "PASS: 启动后无需点击即显示登录页（LoginPage 元素已在初始 UIA 树中）"
    exit 0
} else {
    Write-Host "FAIL: 登录页元素未在启动后 UIA 树中出现"
    exit 1
}
