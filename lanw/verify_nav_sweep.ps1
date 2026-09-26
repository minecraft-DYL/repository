# ============================================================================
# Integration nav sweep: open EVERY NavigationView page in one run and prove
# the app survives, renders content and never shows a literal {x:Bind.
# ASCII-only source; Chinese labels come from char codes.
# UI Automation only - the physical mouse/keyboard are never touched.
#
# Robustness note: WinUI creates/closes popup host windows while navigating, so
# a cached AutomationElement can go stale (ElementNotAvailableException) even
# though the app is fine. Re-acquire the window handle every iteration and load
# the tree with one retry before reporting a failure.
# ============================================================================
$ErrorActionPreference = 'Stop'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'

function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }

$pages = [ordered]@{
    'Home'        = S @(0x4E3B, 0x9875)
    'Accounts'    = S @(0x8D26, 0x53F7, 0x7BA1, 0x7406)
    'Launch'      = S @(0x542F, 0x52A8)
    'Servers'     = S @(0x670D, 0x52A1, 0x5668)
    'Skins'       = S @(0x76AE, 0x80A4)
    'Rental'      = S @(0x79DF, 0x8D41, 0x670D)
    'Proxy'       = S @(0x4EE3, 0x7406)
    'Plugins'     = S @(0x63D2, 0x4EF6)
    'PluginStore' = S @(0x63D2, 0x4EF6, 0x5546, 0x57CE)
    'Chat'        = S @(0x804A, 0x5929)
    'Logs'        = S @(0x65E5, 0x5FD7)
    'Version'     = S @(0x7248, 0x672C)
    'UserHome'    = S @(0x7528, 0x6237, 0x4E2D, 0x5FC3)
    'Settings'    = S @(0x8BBE, 0x7F6E)
}

$exe = 'D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe'
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
if ($hwnd -eq [IntPtr]::Zero) { Write-Host 'FAIL no window'; exit 1 }
Start-Sleep -Seconds 6

$bad = @()
Write-Host ("{0,-12} {1,-8} {2,-7} {3,-7} {4}" -f 'PAGE', 'ELEMS', 'ALIVE', 'XBIND', 'SAMPLE')
foreach ($kv in $pages.GetEnumerator()) {
    $alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
    if (-not $alive) {
        Write-Host ("{0,-12} {1,-8} {2,-7} {3,-7} {4}" -f $kv.Key, '-', 'NO', '-', 'APP EXITED')
        $bad += "$($kv.Key): app exited"
        break
    }

    $hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 10
    if ($hwnd -eq [IntPtr]::Zero) {
        Write-Host ("{0,-12} {1,-8} {2,-7} {3,-7} {4}" -f $kv.Key, '-', 'yes', '-', 'WINDOW GONE')
        $bad += "$($kv.Key): window gone while process alive"
        continue
    }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    [void](Invoke-LanwByName -Root $root -Name $kv.Value)

    # 等待页面渲染完成。启动期初始化会带来后台任务（缓存预热/验证服务器），固定 2.2 秒可能
    # 读到半成品页面——曾把「代理」页误判为 0 元素（实测该页有 60 个元素）。改为轮询取最丰富快照。
    $items = $null
    $best = @()
    for ($tick = 1; $tick -le 10; $tick++) {
        Start-Sleep -Milliseconds 800
        try {
            $snap = @(Get-LanwTexts -Root $root)
        } catch {
            $hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 10
            if ($hwnd -ne [IntPtr]::Zero) { $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd) }
            continue
        }

        if ($snap.Count -gt $best.Count) { $best = $snap }
        if ($snap.Count -ge 8) { $items = $snap; break }
    }

    if ($null -eq $items -and $best.Count -gt 0) { $items = $best }

    if ($null -eq $items) {
        Write-Host ("{0,-12} {1,-8} {2,-7} {3,-7} {4}" -f $kv.Key, '-', 'yes', '-', 'UIA TREE UNAVAILABLE (2 tries)')
        $bad += "$($kv.Key): UIA tree unavailable after retry"
        continue
    }

    $names = @($items | ForEach-Object { $_.Name })
    $xbind = @($names | Where-Object { $_.Contains('x:Bind') }).Count
    $sample = @($names | Where-Object { $_.Length -gt 3 -and $_.Length -lt 26 -and -not $_.Contains('x:Bind') } | Select-Object -First 2) -join ' / '
    $flag = 'ok'
    if ($xbind -gt 0) { $flag = 'LITERAL'; $bad += "$($kv.Key): literal x:Bind" }
    if ($items.Count -lt 8) { $flag = "$flag+THIN"; $bad += "$($kv.Key): only $($items.Count) elements" }

    Write-Host ("{0,-12} {1,-8} {2,-7} {3,-7} {4}  [{5}]" -f $kv.Key, $items.Count, 'yes', $xbind, $sample, $flag)
}

Write-Host ''
if ($bad.Count -eq 0) {
    Write-Host "SUMMARY nav-sweep PASS - all $($pages.Count) pages opened, none crashed, no literal bindings."
    $code = 0
} else {
    Write-Host "SUMMARY nav-sweep FAIL:"
    $bad | ForEach-Object { Write-Host "  - $_" }
    $code = 1
}
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
exit $code
