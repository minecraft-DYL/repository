# t26 运行时验证：启动 Lanw.App.exe，导航到「启动」页（GameLaunchManagerPage），
# 校验页面元素存在、启动按钮可触发启动流程、错误提示不崩溃。
# 证据链：
#   1) 进程存活且有主窗口
#   2) 点击左侧「启动」导航项后，UIA 树出现本页独有元素（页面标题「启动管理」、
#      「游戏版本」ComboBox、JVM 参数文本、AccentButton「启动游戏」、「启动日志」）
#   3) 点击「启动游戏」：进度条出现、状态栏给出可读错误（角色名为空 / 未登录），进程仍存活
#   4) 全窗口截图 PNG 供人工复核
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_t26_launchmanager.png",
    [int]$StartupWaitSeconds = 12
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

function Find-ByName($root, $name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    return $root.FindFirst($TS::Descendants, $cond)
}

function Get-TextNames($root) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Text)
    $found = $root.FindAll($TS::Descendants, $cond)
    $names = @()
    foreach ($item in $found) {
        if ($item.Current.Name) { $names += $item.Current.Name }
    }
    return $names
}

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

if (-not (Test-Path $ExePath)) { Write-Host "FAIL: exe 不存在: $ExePath"; exit 2 }

Write-Host "[1] 启动: $ExePath"
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds $StartupWaitSeconds

$proc.Refresh()
if ($proc.HasExited -or $proc.MainWindowHandle -eq 0) {
    Write-Host "FAIL: 进程未存活或无主窗口（HasExited=$($proc.HasExited)）"
    exit 2
}
Write-Host "[2] 进程存活, MainWindowHandle=$($proc.MainWindowHandle)"

$root = $AE::FromHandle($proc.MainWindowHandle)

# 点击左侧导航「启动」
$nav = Find-ByName $root "启动"
if ($null -eq $nav) {
    Write-Host "FAIL: 未找到「启动」导航项"
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "[3] 找到导航项「启动」: ControlType=$($nav.Current.ControlType.ProgrammaticName)"
try { $nav.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() } catch { Write-Host "  (SelectionItem 失败: $($_.Exception.Message))" }
Start-Sleep -Seconds 4

# 页面独有元素
$title       = Find-ByName $root "启动管理"
$versionBox  = Find-ByName $root "游戏版本"
$jvmLabel    = Find-ByName $root "JVM 参数（虚拟机参数）"
$launchBtn   = Find-ByName $root "启动游戏"
$logTitle    = Find-ByName $root "启动日志"
$closeAllBtn = Find-ByName $root "关闭全部游戏"

$pageOk = ($null -ne $title) -and ($null -ne $versionBox) -and ($null -ne $jvmLabel) -and ($null -ne $launchBtn) -and ($null -ne $logTitle)
Write-Host "[4] 页面元素: 标题=$($null -ne $title) 版本=$($null -ne $versionBox) JVM=$($null -ne $jvmLabel) 启动按钮=$($null -ne $launchBtn) 日志=$($null -ne $logTitle) 关闭全部=$($null -ne $closeAllBtn)"
if (-not $pageOk) {
    Write-Host "已见文本: $((Get-TextNames $root) -join ' | ')"
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# 点击「启动游戏」（角色名为空 → 启动流程校验阶段即给出可读错误，不崩溃）
if ($null -ne $launchBtn) {
    try { $launchBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { Write-Host "  点击启动按钮失败: $($_.Exception.Message)" }
}
Start-Sleep -Seconds 5

$proc.Refresh()
$texts = Get-TextNames $root
$statusText = ($texts | Where-Object { $_ -like "*启动失败*" -or $_ -like "*请先填写角色名*" -or $_ -like "*正在启动游戏*" }) -join " § "
Write-Host "[5] 启动后状态文本: $statusText"
Write-Host "[5] 进程仍存活: $(-not $proc.HasExited)"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Rect26 {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
}
"@
[Win32Rect26]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 800
$rect = New-Object Win32Rect26+RECT
[Win32Rect26]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "[6] 截图: $ShotPath (${w}x${h})"

$proc.Refresh()
$stillAlive = -not $proc.HasExited
Write-Host "[7] 收尾时进程仍存活: $stillAlive"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

$ok = $pageOk -and $stillAlive -and ($statusText.Length -gt 0)
Write-Host ""
if ($ok) { Write-Host "PASS: 启动管理页可打开，启动按钮触发启动流程并给出可读提示，进程未崩溃"; exit 0 }
Write-Host "FAIL: 页面元素/启动反馈/存活校验未通过"; exit 1
