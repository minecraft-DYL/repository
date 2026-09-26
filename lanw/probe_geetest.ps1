# ============================================================================
# Probe: "random login" Geetest dialog - which step actually fails?
# The dialog writes its state into a status TextBlock, so reading the texts
# discriminates: WebView2 runtime missing / external script blocked /
# init failed / ready to click.
# ============================================================================
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }

$T_ACCOUNTS = S @(0x8D26, 0x53F7, 0x7BA1, 0x7406)            # accounts nav
$T_RANDOM = S @(0x968F, 0x673A, 0x767B, 0x5F55)              # random login button

$cand = Get-ChildItem -Path 'D:\ku\traecode\lanw\src\App\Lanw.App\bin' -Recurse -Filter 'Lanw.App.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$exe = $cand.FullName
Write-Host ("INFO  exe = " + $exe)
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
Start-Sleep -Seconds 8
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

[void](Invoke-LanwByName -Root $root -Name $T_ACCOUNTS)
Start-Sleep -Seconds 5
$acc = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
Write-Host ("INFO  accounts page elements = " + $acc.Count + " ; has-random-btn=" + (@($acc | Where-Object { $_.Contains($T_RANDOM) }).Count -gt 0))

# click 随机登录 (the button, not the hint text)
$invoked = Invoke-LanwByName -Root $root -Name $T_RANDOM
Write-Host ("INFO  random-login invoked via: " + $invoked)

for ($t = 3; $t -le 15; $t += 3) {
    Start-Sleep -Seconds 3
    $texts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    $status = @($texts | Where-Object { $_.Length -gt 6 -and $_.Length -lt 90 }) -join ' || '
    Write-Host ("t+{0,2}s elems={1}" -f $t, $texts.Count)
    Write-Host ("        " + $status)
}

[void](Save-LanwScreenshot -WindowHandle $hwnd -Path 'D:\ku\traecode\lanw\probe_geetest.png')
$alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
Write-Host ("INFO  alive = " + $alive)
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
