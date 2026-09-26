# ============================================================================
# t35 verify: server detail shows JAVA version + MODs (with ZIP export button)
#
# Requirement change: game launching is NOT ported; clicking a server must
# explain the server's Java/MC version and its mods, with a ZIP export.
#
# This script:
#   1. opens the Servers page and dumps what is actually on it
#   2. clicks the first server entry if one exists, then inspects the detail page
#      for the MOD card (Java version / MOD version / export button / summary)
#   3. reports honestly when the list is empty (the NetEase gateway needs a
#      logged-in game account), which makes the detail page unreachable by UI
# ============================================================================
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }

$T_SERVERS = S @(0x670D, 0x52A1, 0x5668)                          # servers
$T_JAVA = S @(0x4A61, 0x76, 0x61, 0x20, 0x7248, 0x672C)           # "Java 版本"
$T_MODPACK = S @(0x670D, 0x52A1, 0x5668, 0x20, 0x4D, 0x4F, 0x44)  # "服务器 MOD"
$T_EXPORT = S @(0x5BFC, 0x51FA, 0x20, 0x4D, 0x4F, 0x44)           # "导出 MOD"
$T_GAMEVER = S @(0x6E38, 0x620F, 0x7248, 0x672C)                  # game version
$fail = 0

$cand = Get-ChildItem -Path 'D:\ku\traecode\lanw\src\App\Lanw.App\bin' -Recurse -Filter 'Lanw.App.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$exe = $cand.FullName
Write-Host ("INFO  exe = " + $exe)
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
Start-Sleep -Seconds 7
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

# --- 1. servers list -------------------------------------------------------
[void](Invoke-LanwByName -Root $root -Name $T_SERVERS)
Start-Sleep -Seconds 5
$listTexts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
Write-Host ("INFO  servers page elements = " + $listTexts.Count)
$listTexts | Where-Object { $_.Length -gt 1 -and $_.Length -lt 50 } | Select-Object -First 20 | ForEach-Object { "      | " + $_ }

# clickable server rows: exclude the left NavigationView items (they are ListItems too,
# living at X < 200; page content rows sit further right)
$clicked = $false
$items = $null
try { $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) } catch { }
$all = @($items | Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem })
$candidates = @($all | Where-Object { $_.Current.BoundingRectangle.X -gt 200 })
Write-Host ("INFO  ListItem total = " + $all.Count + " ; nav-pane = " + ($all.Count - $candidates.Count) + " ; page rows = " + $candidates.Count)

if ($candidates.Count -gt 0) {
    $first = $candidates[0]
    $first.SetFocus()
    Start-Sleep -Milliseconds 500
    $invoked = Invoke-LanwElement -Element $first
    $clicked = $true
    Write-Host ("INFO  first server invoked via " + $invoked)
} else {
    Write-Host "INFO  no server rows to click"
}

if ($clicked) {
    Start-Sleep -Seconds 6
    $detail = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    Write-Host "INFO  detail page elements = $($detail.Count)"
    $detail | Where-Object { $_.Length -gt 1 -and $_.Length -lt 60 } | Select-Object -First 25 | ForEach-Object { "      | " + $_ }

    $hasModCard = @($detail | Where-Object { $_.Contains($T_MODPACK) }).Count -gt 0
    $hasExport = @($detail | Where-Object { $_.Contains($T_EXPORT) }).Count -gt 0
    $hasJava = @($detail | Where-Object { $_.Contains($T_JAVA) }).Count -gt 0
    $hasGameVer = @($detail | Where-Object { $_.Contains($T_GAMEVER) }).Count -gt 0

    Write-Host ("{0}  {1,-34} {2}" -f $(if ($hasGameVer) { 'PASS' } else { 'FAIL' }), 'detail still shows game version', $hasGameVer)
    Write-Host ("{0}  {1,-34} {2}" -f $(if ($hasModCard) { 'PASS' } else { 'FAIL' }), 'MOD card present', $hasModCard)
    Write-Host ("{0}  {1,-34} {2}" -f $(if ($hasJava) { 'PASS' } else { 'FAIL' }), 'Java version row present', $hasJava)
    Write-Host ("{0}  {1,-34} {2}" -f $(if ($hasExport) { 'PASS' } else { 'FAIL' }), 'ZIP export button present', $hasExport)

    if (-not ($hasModCard -and $hasJava -and $hasExport)) { $fail++ }
    [void](Save-LanwScreenshot -WindowHandle $hwnd -Path 'D:\ku\traecode\lanw\verify_t35_server_mods.png')
} else {
    Write-Host "SKIP  detail page not reachable from UI (server list is empty - the NetEase"
    Write-Host "      gateway requires a logged-in game account, which this profile has not got)"
    Write-Host "      => MOD card verified by compilation only, NOT by UI"
}

$alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
if (-not $alive) { $fail++ }
Write-Host ("{0}  {1,-34} alive={2}" -f $(if ($alive) { 'PASS' } else { 'FAIL' }), 'app alive at end', $alive)

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host ""
Write-Host "SUMMARY t35-server-mods fail=$fail"
if ($fail -eq 0) { Write-Host 'RESULT: PASS' } else { Write-Host 'RESULT: FAIL' }
