# t22 证据补充：dump 主页所有可见文本（UIA Text 元素），用于核对公告卡/版本信息实际内容。
# 导航采用"先 InvokePattern（等价真实点击，会触发 NavigationView.ItemInvoked）→ 再 SelectionItemPattern.Select()"
# 的稳健策略，并轮询等待 HomePage 标记元素出现（规避 UIA Select 不触发 ItemInvoked 的时序问题）。
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe"
)
$ErrorActionPreference = 'Stop'
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 9
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
    foreach ($t in $found) { if ($t.Current.Name) { $out += $t.Current.Name } }
    return $out
}

$homeNav = Find-ByName $root "主页"
if ($null -eq $homeNav) { Write-Host "FAIL: 未找到'主页'导航项"; exit 2 }

# 1) 先 Invoke（点击语义）
$inv = $null
if ($homeNav.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$inv)) {
    Write-Host "[nav] InvokePattern.Invoke()"
    $inv.Invoke()
} else {
    Write-Host "[nav] InvokePattern 不可用，改用 SelectionItemPattern.Select()"
    $sel = $null
    $homeNav.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$sel) | Out-Null
    $sel.Select()
}

# 2) 轮询等待主页标记；未出现则用另一种模式再试一次
$marker = $null
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 1
    $marker = Find-ByName $root "欢迎使用 Lanw 启动器"
    if ($null -ne $marker) { Write-Host "[nav] 主页已出现（第 $($i+1) 次轮询）"; break }
    if ($i -eq 9) {
        $sel2 = $null
        if ($homeNav.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$sel2)) {
            Write-Host "[nav] 兜底 SelectionItemPattern.Select()"
            $sel2.Select()
        }
    }
}
if ($null -eq $marker) { Write-Host "FAIL: 导航到主页失败"; Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue; exit 2 }

# 3) 等待远端数据（fantnel.json / 网易版本）落地
Start-Sleep -Seconds 8

Write-Host "=== 主页 UIA 文本节点 ==="
$all = Get-AllText $root
Write-Host "count=$($all.Count)"
foreach ($n in $all) { Write-Host ("  [{0}] {1}" -f $n.Length, $n) }
$proc.Refresh()
Write-Host "=== 进程存活: $(-not $proc.HasExited) ==="
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
