# t26 最终运行时验证（共享 UiaHelper，绝不移动真实指针/不注入键盘）
# 证据链：
#   1) 通过 UIA 导航到「启动」页，并用该页独有标记元素自证已切页
#   2) 内存滑块经界面设为 8192 → 保存参数 → resources/nirvanaAccount.json 落盘
#   3) 重启应用后滑块读回 8192（LanwConfig 持久化闭环）
#   4) 点「启动游戏」：出现进度/日志反馈与可读错误提示（本机未登录/无 Java 运行时不崩溃），进程存活
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_t26_launchmanager.png"
)
$ErrorActionPreference = 'Stop'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

function Find-ByName($root, $name) {
    return $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)))
}
function Wait-ForName($root, $name, $timeoutSec = 25) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $f = Find-ByName $root $name
        if ($null -ne $f) { return $f }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
function Get-SliderValue($root) {
    $s = $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Slider)))
    if ($null -eq $s) { return '<no-slider>' }
    try { return $s.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value } catch { return '<no-range>' }
}
function Start-App {
    Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    $p = Start-Process -FilePath $ExePath -PassThru
    $h = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 25
    if ($h -eq [IntPtr]::Zero) { Write-Host "FAIL: 未取得主窗口"; exit 2 }
    return @{ Proc = $p; Root = $AE::FromHandle($h); Hwnd = $h }
}
function Goto-LaunchPage($app) {
    $how = Invoke-LanwByName -Root $app.Root -Name '启动'
    Write-Host "[1] 导航「启动」激活方式: $how"
    if ($null -ne (Wait-ForName $app.Root '角色名（必填）' 25)) { return $true }
    return $false
}

# ---------- 第一轮：页面 + 参数保存 ----------
$app = Start-App
$proc = $app.Proc; $root = $app.Root
$onPage = Goto-LaunchPage $app
$markers = @('启动管理', '游戏版本', 'JVM 参数（虚拟机参数）', '启动游戏', '启动日志', '关闭全部游戏')
$found = @{}
foreach ($m in $markers) { $found[$m] = ($null -ne (Find-ByName $root $m)) }
Write-Host "[2] 页面标记: $(($markers | ForEach-Object { "$_=$($found[$_])" }) -join ' ')"
$pageOk = $onPage -and ($markers | Where-Object { -not $found[$_] }).Count -eq 0

$slider = $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Slider)))
$slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(8192)
Start-Sleep -Milliseconds 600
Write-Host "[3] 内存滑块设为: $(Get-SliderValue $root)"

$saveHow = Invoke-LanwByName -Root $root -Name '保存参数'
Start-Sleep -Seconds 2
$saveMsg = ($(Get-LanwTexts -Root $root | Where-Object { $_.Name -like '*保存*' }).Name) -join ' § '
Write-Host "[4] 保存方式=$saveHow 反馈: $saveMsg"

$jsonFile = Get-ChildItem -Path (Split-Path $ExePath) -Recurse -Filter "nirvanaAccount.json" | Select-Object -First 1
$json = if ($jsonFile) { Get-Content -Raw -Encoding UTF8 $jsonFile.FullName } else { '' }
$diskOk = ($json -match '"gameMemory"\s*:\s*8192')
Write-Host "[5] 落盘: $($jsonFile.FullName)"
Write-Host "[5] json: $json"
Write-Host "[5] 内存持久化: $diskOk"

Save-LanwScreenshot -WindowHandle $app.Hwnd -Path $ShotPath | Out-Null
Write-Host "[6] 截图: $ShotPath"

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# ---------- 第二轮：重启后参数仍在 ----------
$app2 = Start-App
$mem2 = $null
if (Goto-LaunchPage $app2) { $mem2 = Get-SliderValue $app2.Root }
Write-Host "[7] 重启后内存滑块: $mem2"

# ---------- 第三轮：启动按钮触发启动流程（本机未登录 → 可读错误、不崩溃） ----------
$launchHow = Invoke-LanwByName -Root $app2.Root -Name '启动游戏'
Start-Sleep -Seconds 6
$texts = (Get-LanwTexts -Root $app2.Root | Where-Object { $_.Name -match '启动失败|尚未登录|请先填写|正在启动|校验|Java' }).Name
Write-Host "[8] 启动按钮激活方式=$launchHow"
Write-Host "[8] 启动流程反馈: $(($texts | Select-Object -Unique) -join ' § ')"
$app2.Proc.Refresh()
$alive2 = -not $app2.Proc.HasExited
Write-Host "[9] 触发启动后进程存活: $alive2"
Stop-Process -Id $app2.Proc.Id -Force -ErrorAction SilentlyContinue

$flowOk = ($texts | Where-Object { $_ -match '启动失败|尚未登录|请先填写' }).Count -gt 0
$restartOk = ($mem2 -eq 8192)
Write-Host ""
Write-Host "自检: 页面=$pageOk 内存落盘=$diskOk 重启保持=$restartOk 启动流程反馈=$flowOk 存活=$alive2"
if ($pageOk -and $diskOk -and $restartOk -and $flowOk -and $alive2) {
    Write-Host "PASS: 页面可打开、参数可设置保存（重启生效）、启动按钮触发启动流程并给出可读提示且不崩溃"
    exit 0
}
Write-Host "FAIL"
exit 1
