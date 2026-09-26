# t24 runtime verification: GameRentalPage + GameRentalDetailPage reachable, list/error state, no crash.
#
# SAFETY (team rule, 2026-09-24): this script must NEVER touch the human's physical mouse/keyboard.
#   It drives the UI exclusively through UI Automation patterns via the shared helper
#   tools\UiaHelper.ps1; at most it posts mouse messages to the app window (PostMessage), which cannot
#   move the system cursor nor click another application.
#   Forbidden APIs here: SetCursorPos, mouse_event, keybd_event, SendInput, SendKeys.
#
# Evidence:
#   A) offline - HTTPS_PROXY points at a dead local port (.NET 10 HttpClient honors it): the rental page must
#                render (its own header) and show the error card with a retry button, process stays alive.
#   B) online  - no proxy: page must render and show list content or a clear error/empty state; process alive.
#   C) detail  - best effort (informational only): when the online list renders a card, activate it through
#                UIA and check the detail page renders. Not part of PASS/FAIL because a card requires network.
#
# ASCII-only source on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI, so Chinese literals are
# built from code points below.
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$OfflineShot = "D:\ku\traecode\lanw\verify_t24_rental_offline.png",
    [string]$OnlineShot = "D:\ku\traecode\lanw\verify_t24_rental_online.png"
)

$ErrorActionPreference = 'Stop'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'

function S([int[]]$codes) { return (-join ($codes | ForEach-Object { [char]$_ })) }

# Page-unique / state marker strings (code points)
$NAV     = S @(0x79DF, 0x8D41, 0x670D)                                                      # rental nav item
$HEADER  = S @(0x79DF, 0x8D41, 0x670D, 0x7BA1, 0x7406)                                      # "rental guan-li" (page-unique)
$ERR1    = S @(0x65E0, 0x6CD5, 0x83B7, 0x53D6, 0x79DF, 0x8D41, 0x670D, 0x5217, 0x8868)      # cannot fetch rental list
$ERR2    = S @(0x83B7, 0x53D6, 0x79DF, 0x8D41, 0x670D, 0x5217, 0x8868, 0x5931, 0x8D25)      # fetch rental list failed
$EMPTY1  = S @(0x6CA1, 0x6709, 0x53EF, 0x5C55, 0x793A, 0x7684, 0x79DF, 0x8D41, 0x670D)      # no rental to show
$EMPTY2  = S @(0x6CA1, 0x6709, 0x83B7, 0x53D6, 0x5230, 0x79DF, 0x8D41, 0x670D, 0x6570, 0x636E) # no rental data fetched
$RUNNING = S @(0x8FD0, 0x884C, 0x4E2D)                                                      # ServerOn
$OFFSTAT = S @(0x5DF2, 0x5173, 0x670D)                                                      # ServerOff
$PUBLIC  = S @(0x516C, 0x5F00)                                                              # EnumVisibilityStatus.Public
$COUNT   = S @(0x5171, 0x20)                                                                # "gong " (result summary)
$BACK    = S @(0x8FD4, 0x56DE)                                                              # detail: back button
$DINFO   = S @(0x670D, 0x52A1, 0x5668, 0x4FE1, 0x606F)                                      # detail: server info section
$DPLAYER = S @(0x73A9, 0x5BB6, 0x5217, 0x8868)                                              # detail: player list section
$DADDR   = S @(0x670D, 0x52A1, 0x5668, 0x5730, 0x5740)                                      # detail: address section
$DERR    = S @(0x83B7, 0x53D6, 0x79DF, 0x8D41, 0x670D, 0x8BE6, 0x60C5, 0x5931, 0x8D25)      # detail: fetch failed card

function Stop-Lanw {
    Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
}

function Find-Contains($root, $fragment) {
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) { $n = $e.Current.Name; if ($n -and $n.Contains($fragment)) { return $e } }
    return $null
}

function Wait-Contains($root, $fragment, [int]$timeoutSec = 20) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $hit = Find-Contains $root $fragment
        if ($null -ne $hit) { return $hit }
        Start-Sleep -Milliseconds 600
    }
    return $null
}

function Start-Lanw([bool]$offline) {
    if ($offline) {
        $env:HTTPS_PROXY = 'http://127.0.0.1:9'
        $env:HTTP_PROXY = 'http://127.0.0.1:9'
        $env:ALL_PROXY = 'http://127.0.0.1:9'
    } else {
        Remove-Item Env:HTTPS_PROXY -ErrorAction SilentlyContinue
        Remove-Item Env:HTTP_PROXY -ErrorAction SilentlyContinue
        Remove-Item Env:ALL_PROXY -ErrorAction SilentlyContinue
    }

    $proc = Start-Process -FilePath $ExePath -PassThru
    $hwnd = Get-LanwWindowHandle -ProcessId $proc.Id -TimeoutSeconds 30
    if ($hwnd -eq [IntPtr]::Zero) { throw "Lanw.App window not found (HasExited=$($proc.HasExited))" }
    Start-Sleep -Seconds 3
    return [pscustomobject]@{ Proc = $proc; Hwnd = $hwnd; Root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd) }
}

# Activate the rental nav item through UIA patterns (the helper walks inner Text -> owning clickable node) and
# prove the page actually switched by requiring a marker that ONLY the rental page renders.
function Open-RentalPage($app) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $how = Invoke-LanwByName -Root $app.Root -Name $NAV
        if ($null -eq $how) { Write-Host "      nav '$NAV' exposed no UIA pattern"; return $null }
        Write-Host "      nav activated via $how (attempt $attempt)"
        $header = Wait-Contains $app.Root $HEADER 10
        if ($null -ne $header) { return $header }
        Write-Host "      attempt ${attempt}: rental page not rendered yet"
    }
    return $null
}

$results = @()

# ---------- A) offline ----------
Write-Host "[A] offline (HTTPS_PROXY=http://127.0.0.1:9)"
Stop-Lanw
$app = Start-Lanw $true
try {
    $header = Open-RentalPage $app
    $headerOk = $null -ne $header
    $errorElement = Wait-Contains $app.Root $ERR1 30
    if ($null -eq $errorElement) { $errorElement = Wait-Contains $app.Root $ERR2 5 }
    $errorOk = $null -ne $errorElement
    Write-Host "      header=$headerOk errorCard=$errorOk $(if ($errorOk) { "('$($errorElement.Current.Name)')" })"
    $app.Proc.Refresh()
    $alive = -not $app.Proc.HasExited
    Write-Host "      alive=$alive"
    Save-LanwScreenshot -WindowHandle $app.Hwnd -Path $OfflineShot | Out-Null
    Write-Host "      shot: $OfflineShot"
    $results += [pscustomobject]@{ Phase = 'A-offline'; Header = $headerOk; Marker = $errorOk; Alive = $alive }
} finally {
    Stop-Lanw
}

# ---------- B) online (+ C detail best effort) ----------
Write-Host ""
Write-Host "[B] online"
$app = Start-Lanw $false
$detailNote = 'skipped(no card)'
try {
    $header = Open-RentalPage $app
    $headerOk = $null -ne $header

    $probes = @(
        @{ Fragment = $RUNNING; Kind = 'list-running' },
        @{ Fragment = $OFFSTAT; Kind = 'list-offline' },
        @{ Fragment = $PUBLIC;  Kind = 'list-public' },
        @{ Fragment = $COUNT;   Kind = 'list-count' },
        @{ Fragment = $ERR1;    Kind = 'error' },
        @{ Fragment = $ERR2;    Kind = 'error' },
        @{ Fragment = $EMPTY1;  Kind = 'empty' },
        @{ Fragment = $EMPTY2;  Kind = 'empty' })
    $markerKind = ''
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline -and $markerKind -eq '') {
        foreach ($probe in $probes) {
            if ($null -ne (Find-Contains $app.Root $probe.Fragment)) { $markerKind = $probe.Kind; break }
        }
        if ($markerKind -eq '') { Start-Sleep -Milliseconds 700 }
    }
    Write-Host "      header=$headerOk marker=$markerKind"
    Save-LanwScreenshot -WindowHandle $app.Hwnd -Path $OnlineShot | Out-Null
    Write-Host "      shot: $OnlineShot"

    # C) detail page (informational only): activate the first list item through UIA when the list rendered.
    if ($markerKind -like 'list-*') {
        $all = $app.Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $card = $null
        foreach ($e in $all) {
            if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem' -and $e.Current.IsEnabled) { $card = $e; break }
        }
        if ($null -ne $card) {
            $how = Invoke-LanwElement $card
            if ($null -eq $how) {
                $ok = Send-LanwClickNoCursor -WindowHandle $app.Hwnd -Element $card
                $how = "Send-LanwClickNoCursor=$ok"
            }
            $back = Wait-Contains $app.Root $BACK 15
            $section = $null
            foreach ($frag in @($DINFO, $DPLAYER, $DADDR, $DERR)) {
                $section = Wait-Contains $app.Root $frag 20
                if ($null -ne $section) { break }
            }
            $detailNote = "via=$how back=$($null -ne $back) section=$(if ($null -ne $section) { "'$($section.Current.Name)'" } else { 'none' })"
            Write-Host "      detail: $detailNote"
        } else {
            $detailNote = 'skipped(no list item found)'
            Write-Host "      detail: $detailNote"
        }
    }

    $app.Proc.Refresh()
    $alive = -not $app.Proc.HasExited
    Write-Host "      alive=$alive"
    $results += [pscustomobject]@{ Phase = 'B-online'; Header = $headerOk; Marker = ($markerKind -ne ''); Alive = $alive }
} finally {
    Stop-Lanw
}

Write-Host ""
$results | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "detail phase (informational): $detailNote"

$failed = @($results | Where-Object { -not $_.Header -or -not $_.Marker -or -not $_.Alive })
if ($results.Count -eq 2 -and $failed.Count -eq 0) {
    Write-Host "PASS: GameRentalPage reachable via UIA; offline shows the error state without crashing; online shows list or a clear error/empty state"
    exit 0
} else {
    Write-Host "FAIL: see table above"
    exit 1
}
