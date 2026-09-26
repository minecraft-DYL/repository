# ============================================================================
# Lanw serialized build/test wrapper.  (ASCII-only on purpose: Windows
# PowerShell 5.1 reads .ps1 as ANSI, so non-ASCII literals corrupt the parse.)
#
# WHY: 8 members building the SAME project at the same time fight over the same
# obj/bin files. A 40s build degrades into 10+ minutes of lock contention, the
# agent turn gets interrupted while waiting, is retried, and spawns yet another
# concurrent build. The whole team then stalls with zero output.
# This wrapper takes a MACHINE-WIDE named mutex first, so exactly one lanw
# build/test runs at a time, and disables MSBuild node reuse and the Roslyn
# build server (they leak worker processes when a turn is interrupted).
#
# USAGE
#   & 'D:\ku\traecode\lanw\tools\Build-Lanw.ps1' -Project src\App\Lanw.App\Lanw.App.csproj
#   & 'D:\ku\traecode\lanw\tools\Build-Lanw.ps1' -Verb test -Project tests\Lanw.Core.Tests\Lanw.Core.Tests.csproj -OutDir D:\ku\traecode\lanw\.outs\dev-1\
#
# Never call bare `dotnet build` / `dotnet test` for lanw projects.
# ============================================================================
param(
    [string]$Project = 'src\App\Lanw.App\Lanw.App.csproj',
    [ValidateSet('build', 'test', 'publish')][string]$Verb = 'build',
    [string]$Configuration = 'Debug',
    [string]$OutDir = '',
    [int]$TimeoutMinutes = 25,
    [switch]$NoRestore,
    [switch]$Quiet
)
$ErrorActionPreference = 'Continue'
$root = 'D:\ku\traecode\lanw'
$full = Join-Path $root $Project
if (-not (Test-Path $full)) { Write-Host "[Build-Lanw] project not found: $full"; exit 3 }

$mutex = New-Object System.Threading.Mutex($false, 'Global\lanw-serialized-build')
$acquired = $false
$wait = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $acquired = $mutex.WaitOne([TimeSpan]::FromMinutes($TimeoutMinutes))
    if (-not $acquired) {
        Write-Host "[Build-Lanw] waited over $TimeoutMinutes min for the build lock; giving up (another build may be stuck - report to captain)"
        exit 2
    }
    $wait.Stop()
    if ($wait.Elapsed.TotalSeconds -gt 2) {
        Write-Host ("[Build-Lanw] queued {0}s for the build lock" -f [math]::Round($wait.Elapsed.TotalSeconds, 1))
    }

    $a = @($Verb, $full, '-c', $Configuration, '-m:1', '-nodeReuse:false', '--disable-build-servers')
    if ($NoRestore) { $a += '--no-restore' }
    if ($OutDir) { $a += "-p:OutDir=$OutDir" }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $out = & dotnet @a 2>&1
    $code = $LASTEXITCODE
    $sw.Stop()

    $errs = @($out | Select-String -Pattern ': error ')
    if (-not $Quiet) {
        if ($errs.Count -gt 0) {
            Write-Host "[Build-Lanw] ==== ALL ERRORS ($($errs.Count) lines) ===="
            $errs | ForEach-Object { Write-Host $_.Line.Trim() }
        }
        $out | Select-String -Pattern 'error|warning|Build succeeded|Build FAILED|Passed!|Failed!|Total tests' | Select-Object -Last 4 | ForEach-Object { Write-Host ("[Build-Lanw] " + $_.Line.Trim()) }
        # Always echo the raw tail: the test summary is localized, so a grep can miss it.
        $out | Where-Object { $_ -match '\S' } | Select-Object -Last 6 | ForEach-Object { Write-Host ("[Build-Lanw] tail| " + $_.Trim()) }
    }
    Write-Host ("[Build-Lanw] {0} exit={1} elapsed={2}s errors={3}" -f $Verb, $code, [math]::Round($sw.Elapsed.TotalSeconds, 1), $errs.Count)
    exit $code
}
finally {
    if ($acquired) { try { $mutex.ReleaseMutex() } catch { } }
    try { $mutex.Dispose() } catch { }
}
