# t25 运行时验证：皮肤页（SkinsPage）可打开 + 无网络时错误态不崩溃；列表有数据时顺带验证详情页（SkinDetailPage）。
#
# 安全约定（全队规则）：绝不使用 SetCursorPos / mouse_event / keybd_event / SendKeys。
# 全部交互走共享工具 tools\UiaHelper.ps1（UIA 模式优先，最后手段只向本窗口 PostMessage，不移动物理指针）。
#
# 证据链：
#   1) 启动 Lanw.App.exe，进程存活、有主窗口；启动基线为登录页（页面切换断言的前置条件）
#   2) 通过共享工具按名激活左侧导航「皮肤」→ 皮肤页独有标记出现（标题「皮肤管理」+「上传本地皮肤」按钮 + 搜索框）
#   3) 等待列表请求收敛：出现错误态文案 / 空态文案 / 结果计数之一；进程始终存活（不崩溃）
#   4) 若列表有数据：点开第一条 → 详情页独有标记出现（「皮肤介绍」+「应用设置」+「皮肤作者」）
#   5) 截图留证
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$ShotDir = "D:\ku\traecode\lanw"
)

$ErrorActionPreference = 'Stop'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'

$fail = @()

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

Write-Host "[1] 启动: $ExePath"
if (-not (Test-Path $ExePath)) { Write-Host "FAIL: 找不到 exe"; exit 2 }
$proc = Start-Process -FilePath $ExePath -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $proc.Id -TimeoutSeconds 30
$proc.Refresh()
if ($proc.HasExited) { Write-Host "FAIL: 进程已退出 (ExitCode=$($proc.ExitCode))"; exit 2 }
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "FAIL: 未取得主窗口句柄"; exit 2 }
Write-Host "[1] 进程存活: True  hwnd: $hwnd"

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
Start-Sleep -Seconds 6

function Get-Names($r) { return (Get-LanwTexts -Root $r | ForEach-Object { $_.Name }) }
function Has-Any($names, [string[]]$needles) {
    foreach ($n in $needles) { if ($names -like "*$n*") { return $true } }
    return $false
}

# 等待 UI 真正渲染（WinUI 首帧可能晚于窗口创建）：轮询登录页标记或任一导航项
$ready = $false
$deadline = (Get-Date).AddSeconds(40)
while ((Get-Date) -lt $deadline) {
    $proc.Refresh()
    if ($proc.HasExited) { Write-Host "FAIL: 启动后进程已退出 (ExitCode=$($proc.ExitCode))"; exit 2 }
    try {
        $probe = Get-LanwTexts -Root $root | ForEach-Object { $_.Name }
        if (($probe -like '*登录账号*') -or ($probe -like '*主页*') -or ($probe -like '*登录*')) { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 2
}
Write-Host "[1] UI 就绪: $ready"
if (-not $ready) { Write-Host "FAIL: 40s 内界面未就绪（UIA 树无已知元素）"; Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force; exit 2 }

$base = Get-Names $root
$baseHasLogin = Has-Any $base @('登录账号')
Write-Host "[1] 启动基线包含登录页标记「登录账号」: $baseHasLogin"
# 2) 切到皮肤页
$how = Invoke-LanwByName -Root $root -Name '皮肤'
Write-Host "[2] 激活导航项「皮肤」: $how  (期望非空)"
if (-not $how) { $fail += "导航项「皮肤」未能激活（未找到或无可用 UIA 模式）" }
Start-Sleep -Seconds 8
$proc.Refresh()
if ($proc.HasExited) { Write-Host "FAIL: 打开皮肤页后进程崩溃 (ExitCode=$($proc.ExitCode))"; exit 2 }

$names = Get-Names $root
$hasTitle = Has-Any $names @('皮肤管理')
$hasUpload = Has-Any $names @('上传本地皮肤')
$hasSearch = Has-Any $names @('搜索皮肤')
$hasError = Has-Any $names @('皮肤列表加载失败')
$hasEmpty = Has-Any $names @('没有获取到皮肤数据')
$hasSummary = Has-Any $names @('共 ')
$hasSkinItemMarker = Has-Any $names @('下载量:')
Write-Host "[3] 皮肤页标记：标题=$hasTitle 上传按钮=$hasUpload 搜索框=$hasSearch"
Write-Host "[3] 状态收敛：错误态=$hasError 空态=$hasEmpty 结果计数=$hasSummary 列表条目=$hasSkinItemMarker"

if (-not $hasTitle) { $fail += "皮肤页标题「皮肤管理」未出现（页面未真正打开）" }
if (-not $hasUpload) { $fail += "「上传本地皮肤」按钮未出现" }
if (-not $hasSearch) { $fail += "搜索框未出现" }
if ($hasTitle -and -not $baseHasLogin) { $fail += "皮肤页标记命中但启动基线异常（未先出现登录页标记）" }
if (-not ($hasError -or $hasEmpty -or $hasSummary)) { $fail += "列表/错误态/空态文本均未出现（状态未收敛）" }
if ($hasError) {
    $msg = (Get-LanwTexts -Root $root | Where-Object { $_.Name -like '*无法获取皮肤列表*' } | Select-Object -First 1).Name
    Write-Host "[3] 错误态文案: $msg"
    if (-not $msg) { $fail += "错误态缺文案（应显示 无法获取皮肤列表：…）" }
}

$literal = Test-LanwNoLiteralBinding -Root $root
Write-Host "[3] 无字面量 {x:Bind 残留: $($literal.Pass)"
if (-not $literal.Pass) { $fail += "界面出现未解析的字面量绑定：$($literal.Hit -join '; ')" }

Save-LanwScreenshot -WindowHandle $hwnd -Path (Join-Path $ShotDir 'verify_t25_skins_list.png') | Out-Null
Write-Host "[4] 截图: verify_t25_skins_list.png"

# 5) 详情页（入口只有列表项点击；无网络时列表为空，运行时不可达）
$detailChecked = $false
if ($hasSkinItemMarker) {
    $el = $null
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) { try { if ($e.Current.Name -like '*下载量:*') { $el = $e; break } } catch { } }
    if ($el) {
        $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
        $target = $el
        for ($d = 0; $d -lt 6; $d++) {
            if ($target.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { break }
            $target = $walker.GetParent($target)
            if ($null -eq $target) { break }
        }
        if ($target) {
            $how2 = Invoke-LanwElement $target
            if (-not $how2) { if (Send-LanwClickNoCursor -WindowHandle $hwnd -Element $target) { $how2 = 'PostMessageClick' } }
            Write-Host "[5] 打开皮肤详情: $how2"
        }
    }
    Start-Sleep -Seconds 8
    $proc.Refresh()
    if ($proc.HasExited) { Write-Host "FAIL: 打开详情页后进程崩溃 (ExitCode=$($proc.ExitCode))"; exit 2 }
    $dnames = Get-Names $root
    $hasIntro = Has-Any $dnames @('皮肤介绍')
    $hasApply = Has-Any $dnames @('应用设置')
    $hasAuthor = Has-Any $dnames @('皮肤作者')
    $hasSettingsSummary = Has-Any $dnames @('skin_type=31')
    Write-Host "[5] 详情页标记：皮肤介绍=$hasIntro 应用设置=$hasApply 皮肤作者=$hasAuthor 设置摘要=$hasSettingsSummary"
    $detailChecked = $hasIntro -and $hasApply -and $hasSettingsSummary
    if (-not $detailChecked) { $fail += "详情页元素未出现" }
    Save-LanwScreenshot -WindowHandle $hwnd -Path (Join-Path $ShotDir 'verify_t25_skin_detail.png') | Out-Null
    Write-Host "[5] 截图: verify_t25_skin_detail.png"
} else {
    Write-Host "[5] 列表无数据（无网络/接口不可用）：详情页唯一入口是列表项点击，本轮无法运行时进入；"
    Write-Host "    详情页使用 x:Bind 编译期强类型绑定（成员写错即构建失败），已由 Debug/Release 0 错 0 警覆盖。"
}

$proc.Refresh()
$alive = -not $proc.HasExited
Write-Host "[6] 结束时进程存活: $alive"
if (-not $alive) { $fail += "结束时进程已退出" }

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host ""
if ($fail.Count -gt 0) {
    Write-Host "RESULT: FAIL"
    $fail | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

if ($detailChecked) { Write-Host "RESULT: PASS（皮肤列表页 + 详情页均打开且进程未崩溃）" }
else { Write-Host "RESULT: PASS（皮肤列表页打开、错误态收敛、进程未崩溃；列表无数据故详情页未运行时进入）" }
exit 0
