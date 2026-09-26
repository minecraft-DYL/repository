# t25 诊断：点击导航「皮肤」后 dump 皮肤页全部 UIA 文本元素（确认错误态文案与页面结构，非崩溃）。
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$OutFile = "D:\ku\traecode\lanw\verify_t25_skins_uia_dump.txt"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 12
$proc.Refresh()
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::AutoamtionIdProperty, 'skinsItem')
$skins = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'skinsItem')))
if ($null -ne $skins) {
    try { $skins.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() } catch { }
    Write-Host "已点击导航「皮肤」"
} else { Write-Host "未找到 skinsItem" }

Start-Sleep -Seconds 10
$proc.Refresh()
Write-Host "进程存活: $(-not $proc.HasExited)"

$lines = New-Object System.Collections.Generic.List[string]
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
$lines.Add("total elements: $($all.Count)")
foreach ($el in $all) {
    try {
        $nm = $el.Current.Name
        if ($nm) { $lines.Add(("{0} | '{1}' | autoId='{2}'" -f $el.Current.ControlType.ProgrammaticName, $nm, $el.Current.AutomationId)) }
    } catch { }
}
$lines | Set-Content -Encoding UTF8 $OutFile
Write-Host "dump -> $OutFile"

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
