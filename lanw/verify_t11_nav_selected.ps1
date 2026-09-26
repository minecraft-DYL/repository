# t11 运行时验证（补充）：启动后 NavigationView 的"登录"导航项应处于选中状态。
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe"
)

$ErrorActionPreference = 'Stop'
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 9
$proc.Refresh()
if ($proc.HasExited) { Write-Host "FAIL: 进程提前退出"; exit 2 }

Add-Type -AssemblyName UIAutomationClient
$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)

# 列出所有 ListItem，展示名称与选中状态
$listItemCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ListItem)
$items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $listItemCond)

$loginSelected = $false
foreach ($it in $items) {
    $name = $it.Current.Name
    $sel = $false
    if ($it.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$null)) {
        $pattern = $null
        if ($it.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
            $sel = $pattern.Current.IsSelected
        }
    }
    Write-Host ("导航项: '{0}'  IsSelected={1}" -f $name, $sel)
    if ($name -eq '登录' -and $sel) { $loginSelected = $true }
}

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

if ($loginSelected) {
    Write-Host "PASS: '登录'导航项在启动后即被选中"
    exit 0
} else {
    Write-Host "FAIL: '登录'导航项未选中（或未找到）"
    exit 1
}
