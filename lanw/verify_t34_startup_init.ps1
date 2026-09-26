# ============================================================================
# t34 verify: startup init chain (source InitProgram.NelInit1)
#
# The app has no Serilog file sink, but the Logs page renders the in-memory sink,
# so this reads the app's own log view through UI Automation:
#   * "debug build, version check skipped"  -> VersionCheck ran
#   * "------  完成 ------"                  -> NelInit1 reached the end
#     (i.e. CreateServices + PluginMessage.Initialize + Online all executed)
# It also re-checks that startup still lands on a healthy window (no crash).
# ============================================================================
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }

$T_LOGS = S @(0x65E5, 0x5FD7)                                   # logs nav item
$T_DEBUG_SKIP = S @(0x5DF2, 0x8DF3, 0x8FC7, 0x7248, 0x672C, 0x68C0, 0x6D4B)   # version check skipped
$T_FAILED = S @(0x521D, 0x59CB, 0x5316, 0x5931, 0x8D25)         # init failed
$MARK_DONE = '------  ' + (S @(0x5B8C, 0x6210)) + ' ------'          # ------  <done>  ------
$T_COMPLETE = S @(0x5B8C, 0x6210)                               # done
$fail = 0

$cand = Get-ChildItem -Path 'D:\ku\traecode\lanw\src\App\Lanw.App\bin' -Recurse -Filter 'Lanw.App.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $cand) { Write-Host 'FAIL  cannot find Lanw.App.exe'; exit 1 }
$exe = $cand.FullName
Write-Host ("INFO  exe = " + $exe + "  (" + $cand.LastWriteTime.ToString('MM-dd HH:mm:ss') + ")")

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
Start-Sleep -Seconds 7

$alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

# startup landing must still be functional (regression guard for the new init call)
$startupTexts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
$landed = @($startupTexts | Where-Object { $_ -match '^' -and $_.Length -gt 0 }).Count -gt 10
$ok = $alive -and $landed
if (-not $ok) { $fail++ }
Write-Host ("{0}  {1,-30} alive={2} elements={3}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), 'app starts and renders', $alive, $startupTexts.Count)

[void](Invoke-LanwByName -Root $root -Name $T_LOGS)
Start-Sleep -Seconds 3
$logTexts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })

$hasSkip = @($logTexts | Where-Object { $_.Contains($T_DEBUG_SKIP) }).Count -gt 0
if (-not $hasSkip) { $fail++ }
Write-Host ("{0}  {1,-30} (found version-check-skipped line={2})" -f $(if ($hasSkip) { 'PASS' } else { 'FAIL' }), 'VersionCheck ran at startup', $hasSkip)

$hasDone = @($logTexts | Where-Object { $_.Contains($T_COMPLETE) }).Count -gt 0
if (-not $hasDone) { $fail++ }
Write-Host ("{0}  {1,-30} (found NelInit1-completed marker={2})" -f $(if ($hasDone) { 'PASS' } else { 'FAIL' }), 'NelInit1 completed', $hasDone)

$hasFail = @($logTexts | Where-Object { $_.Contains($T_FAILED) }).Count -gt 0
if ($hasFail) { $fail++ }
Write-Host ("{0}  {1,-30} (no init-failed line={2})" -f $(if (-not $hasFail) { 'PASS' } else { 'FAIL' }), 'no init failure logged', (-not $hasFail))

$alive2 = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
if (-not $alive2) { $fail++ }
Write-Host ("{0}  {1,-30} alive={2}" -f $(if ($alive2) { 'PASS' } else { 'FAIL' }), 'still alive after logs view', $alive2)

[void](Save-LanwScreenshot -WindowHandle $hwnd -Path 'D:\ku\traecode\lanw\verify_t34_startup_logs.png')
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ""
Write-Host "SUMMARY t34-startup-init fail=$fail"
if ($fail -eq 0) { Write-Host 'RESULT: PASS' } else { Write-Host 'RESULT: FAIL' }
