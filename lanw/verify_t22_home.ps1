# t22 运行时验证：启动 exe → UIA 导航到"主页" → 校验公告卡片 / 版本信息 / 随机名生成器。
# 证据链：
#   1) 进程存活且主窗口存在
#   2) 选中"主页"导航项后 HomePage 独有元素出现（欢迎标题 / 公告 / 版本信息 / 随机名生成器）
#   3) 公告卡三张 + 版本信息两行标签可见；远端数据是否落地（fantnel.json / 网易版本）作为信息项报告
#   4) 点击"生成随机名" → 出现 6 中文字 + 1~3 数字 的 7~9 位结果（同时证明 UI 线程未被死循环冻结）
#   5) 生成后点击"复制" → 状态栏更新为"已复制到剪贴板"（UI 线程仍可响应）
#   6) 全窗口截图留证
# 说明：第 3 项远端数值需要联网；离线时展示占位内容仍满足"公告与版本信息可见"，故不作为失败门槛，
#       仅第 2/4/5 项为门槛（随机名一项覆盖 RandomNameUtil 死循环回归）。
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_t22_home.png"
)

$ErrorActionPreference = 'Stop'
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

Write-Host "[1] 启动: $ExePath"
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 9
$proc.Refresh()
if ($proc.HasExited -or $proc.MainWindowHandle -eq 0) { Write-Host "FAIL: 进程未存活或无主窗口"; exit 2 }
Write-Host "[2] 进程存活 MainWindowHandle: $($proc.MainWindowHandle)"

Add-Type -AssemblyName UIAutomationClient
$UIA = [System.Windows.Automation.AutomationElement]
$root = $UIA::FromHandle($proc.MainWindowHandle)

function Find-ByName($element, $name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, $name)
    return $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Get-AllText($element) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        $UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    $found = $element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $out = @()
    foreach ($t in $found) { $out += $t.Current.Name }
    return $out
}

# [3] 导航到"主页"（启动缺省页为登录页，需导航）
# 注意：NavigationViewItem 上 InvokePattern 通常不可用，而 SelectionItemPattern.Select() 的生效存在时序抖动
#      （启动初始导航可能后到并覆盖），故采用"先 Invoke → 否则 Select → 轮询等待 HomePage 标记 → 兜底再 Select"。
$homeNav = Find-ByName $root "主页"
if ($null -eq $homeNav) { Write-Host "FAIL: 未找到'主页'导航项"; exit 2 }
$navInvoke = $null
if ($homeNav.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$navInvoke)) {
    $navInvoke.Invoke()
} else {
    $selPattern = $null
    if (-not $homeNav.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selPattern)) {
        Write-Host "FAIL: '主页'导航项不支持 SelectionItemPattern/InvokePattern"; exit 2
    }
    $selPattern.Select()
}

$homeMarker = $null
$homePoll = 0
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 1
    $homePoll = $i + 1
    $homeMarker = Find-ByName $root "欢迎使用 Lanw 启动器"
    if ($null -ne $homeMarker) { break }
    if ($i -eq 9) {
        $selRetry = $null
        if ($homeNav.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selRetry)) { $selRetry.Select() }
    }
}
if ($null -eq $homeMarker) {
    Write-Host "FAIL: 导航到主页失败（HomePage 标记元素未出现）"
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    exit 2
}
Write-Host "[3] 已导航到'主页'（第 $homePoll 次轮询命中 HomePage）"

# 等待远端数据（fantnel.json + 网易版本）落地
Start-Sleep -Seconds 14
$proc.Refresh()
if ($proc.HasExited) { Write-Host "FAIL: 进程在主页加载后崩溃"; exit 2 }

# [4] HomePage 独有元素
$welcome = Find-ByName $root "欢迎使用 Lanw 启动器"
$notice  = Find-ByName $root "公告"
$verinfo = Find-ByName $root "版本信息"
$randgen = Find-ByName $root "随机名生成器"
$labelVer = Find-ByName $root "游戏版本"
$labelCrc = Find-ByName $root "CRC 盐值"
Write-Host "[4] 欢迎标题:$($null -ne $welcome) 公告:$($null -ne $notice) 版本信息:$($null -ne $verinfo) 随机名生成器:$($null -ne $randgen)"
Write-Host "[4] 标签 游戏版本:$($null -ne $labelVer) CRC盐值:$($null -ne $labelCrc)"

$textsAfterNav = Get-AllText $root

# [5] 公告卡：三张。离线为占位"广告位 N"，联网为远端名称
$adPlaceholders = @()
foreach ($n in 1..3) { if ($textsAfterNav -contains "广告位 $n") { $adPlaceholders += "广告位 $n" } }
$remoteAdHit = @($textsAfterNav | Where-Object { $_ -and $_ -ne '公告' -and $_ -match '^.{1,40}$' -and ($_ -notmatch '广告位') -and $textsAfterNav.IndexOf($_) -gt 0 })
Write-Host "[5] 公告占位卡: $($adPlaceholders -join '/')"
$verValue = ($textsAfterNav | Where-Object { $_ -match '^\d+\.\d+\.\d+(\.\d+)?$' } | Select-Object -First 1)
$crcValue = ($textsAfterNav | Where-Object { $_ -match '^[A-Za-z0-9_\-]{8,}$' -and $_ -notmatch '^\d+\.\d+\.\d' } | Select-Object -First 1)
Write-Host "[5] 远端数据: 游戏版本='$verValue' CRC盐值='$crcValue'（联网项，离线为占位/未下发）"
$versionVisible = ($null -ne $labelVer) -and (($textsAfterNav | Where-Object { $_ -match '获取中|获取失败|^\d+\.\d+' }).Count -gt 0)
Write-Host "[5] 版本信息区有可见值: $versionVisible"

# [6] 点击"生成随机名"：必须出现 7~9 位结果（同时证明 UI 线程未被 RandomNameUtil 死循环冻结）
$btnCond = New-Object System.Windows.Automation.PropertyCondition(
    $UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
$genBtn = $null
foreach ($b in $btns) { if ($b.Current.Name -eq '生成随机名') { $genBtn = $b; break } }

$randomNameValue = $null
if ($null -ne $genBtn) {
    $invoke = $null
    if ($genBtn.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) {
        $invoke.Invoke()
        Start-Sleep -Seconds 2
        foreach ($n in (Get-AllText $root)) {
            if ($n -match '^[\p{IsCJKUnifiedIdeographs}]{6}\d{1,3}$') { $randomNameValue = $n; break }
        }
    }
}
$randomNameOk = ($null -ne $randomNameValue) -and $randomNameValue.Length -ge 7 -and $randomNameValue.Length -le 9
Write-Host "[6] 随机名按钮:$($null -ne $genBtn) 结果:'$randomNameValue' 有效:$randomNameOk"

# [7] UI 线程仍可响应：点"复制" → 状态栏变为"已复制到剪贴板"
$copyBtn = $null
foreach ($b in $btns) { if ($b.Current.Name -eq '复制') { $copyBtn = $b; break } }
$copyOk = $false
if ($null -ne $copyBtn) {
    $invoke2 = $null
    if ($copyBtn.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke2)) {
        $invoke2.Invoke()
        Start-Sleep -Seconds 2
        $copyOk = ($null -ne (Find-ByName $root "已复制到剪贴板"))
    }
}
Write-Host "[7] 复制按钮:$($null -ne $copyBtn) 状态栏反馈:$copyOk"

# [8] 截图留证
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Rect22 {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
}
"@
[Win32Rect22]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 800
$rect = New-Object Win32Rect22+RECT
[Win32Rect22]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "[8] 截图已保存: $ShotPath (${w}x${h})"

$proc.Refresh()
$stillAlive = -not $proc.HasExited
Write-Host "[9] 全部交互后进程仍存活: $stillAlive"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

$ok = ($null -ne $welcome) -and ($null -ne $notice) -and ($null -ne $verinfo) -and ($null -ne $randgen) `
      -and ($null -ne $labelVer) -and ($null -ne $labelCrc) -and $versionVisible `
      -and $randomNameOk -and $copyOk -and $stillAlive
Write-Host ""
if ($ok) {
    Write-Host "PASS: 主页可见公告卡片与版本信息，随机名生成/复制可用，进程稳定"
    exit 0
} else {
    Write-Host "FAIL: 主页验收未全部通过（见上方各项）"
    exit 1
}
