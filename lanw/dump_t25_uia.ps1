# t25 诊断：dump Lanw.App 主窗口 UIA 树（元素名 + 控件类型），用于确认导航项与皮肤页元素的真实 Name。
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$OutFile = "D:\ku\traecode\lanw\verify_t25_uia_dump.txt"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 12
$proc.Refresh()
if ($proc.HasExited) { Write-Host "进程已退出 ExitCode=$($proc.ExitCode)"; exit 2 }
Write-Host "MainWindowHandle=$($proc.MainWindowHandle)"
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

$lines = New-Object System.Collections.Generic.List[string]
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
$lines.Add("total elements: $($all.Count)")
foreach ($el in $all) {
    try {
        $ct = $el.Current.ControlType.ProgrammaticName
        $nm = $el.Current.Name
        $aid = $el.Current.AutomationId
        if ($nm -or $aid) {
            $lines.Add(("{0} | name='{1}' | autoId='{2}'" -f $ct, $nm, $aid))
        }
    } catch { }
}
$lines | Set-Content -Encoding UTF8 $OutFile
Write-Host "dump -> $OutFile ($($lines.Count) lines)"

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
