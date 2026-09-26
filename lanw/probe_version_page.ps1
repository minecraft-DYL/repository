# ============================================================================
# Focused probe: what happens to the app window when the "Version" page opens?
# ============================================================================
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }
$T_VERSION = S @(0x7248, 0x672C)   # "version"

$exe = 'D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe'
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd1 = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
Start-Sleep -Seconds 6
Write-Host "start: pid=$($p.Id) hwnd=$hwnd1"

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd1)
$how = Invoke-LanwByName -Root $root -Name $T_VERSION
Write-Host "invoked Version nav with: $how"
Start-Sleep -Seconds 4

$alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
$exitCode = 'n/a(running)'
if ($alive) { try { if ($alive.HasExited) { $exitCode = $alive.ExitCode } } catch { } }
Write-Host "process alive: $($null -ne $alive)  exitcode: $exitCode  threads: $(if ($alive) { $alive.Threads.Count } else { 0 })"

Write-Host '--- all top-level windows of that pid ---'
$found = @()
[LanwUiaWin32]::EnumWindows({
        param($h, $l)
        $wpid = 0
        [void][LanwUiaWin32]::GetWindowThreadProcessId($h, [ref]$wpid)
        if ($wpid -eq $p.Id) {
            $r = New-Object LanwUiaWin32+RECT
            [void][LanwUiaWin32]::GetWindowRect($h, [ref]$r)
            $script:found += [pscustomobject]@{
                Handle  = $h
                Visible = [LanwUiaWin32]::IsWindowVisible($h)
                Rect    = "$($r.Left),$($r.Top) $($r.Right - $r.Left)x$($r.Bottom - $r.Top)"
            }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
$found | Format-Table -AutoSize | Out-String | Write-Host

$oldStillOk = $false
try { [void][System.Windows.Automation.AutomationElement]::FromHandle($hwnd1); $oldStillOk = $true } catch { $oldStillOk = $false }
Write-Host "old hwnd=$hwnd1 still usable: $oldStillOk"
$newHwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 5
Write-Host "largest visible hwnd now: $newHwnd"
if ($newHwnd -ne [IntPtr]::Zero) {
    try {
        $root2 = [System.Windows.Automation.AutomationElement]::FromHandle($newHwnd)
        $t = @(Get-LanwTexts -Root $root2 | ForEach-Object { $_.Name })
        Write-Host "texts on new root ($($t.Count)):"
        $t | Select-Object -First 25 | ForEach-Object { Write-Host "   $_" }
    } catch {
        Write-Host "FromHandle/texts failed: $($_.Exception.Message)"
    }
}

Write-Host '--- Lanw log tail (if any) ---'
$logCandidates = @("$env:APPDATA\Lanw", "$env:LOCALAPPDATA\Lanw", 'D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64')
foreach ($d in $logCandidates) {
    if (Test-Path $d) {
        Get-ChildItem -Path $d -Recurse -Include *.log,*.txt -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-10) } |
        Select-Object -First 3 | ForEach-Object {
            Write-Host "== $($_.FullName)"
            Get-Content $_.FullName -Tail 15 | ForEach-Object { Write-Host "   $_" }
        }
    }
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
