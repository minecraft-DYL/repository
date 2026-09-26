# t27 runtime verification: ChatPage reachable, chatEnable switch toggles, send-without-connection
# shows the error state (no crash), section-sign color codes are stripped when bubbles render.
#
# SAFETY: drives the app only through UI Automation patterns via tools/UiaHelper.ps1.
# No SetCursorPos / mouse_event / keybd_event / SendInput / SendKeys anywhere.
#
# ASCII-only source on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI, so Chinese UI
# strings are built from code points below.
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_t27_chat.png"
)

$ErrorActionPreference = 'Stop'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'

function C([int[]]$codes) { return [string]::Concat([char[]]($codes | ForEach-Object { [char]$_ })) }

$NAV_CHAT   = C @(0x804A, 0x5929)                                                             # liao-tian
$HEADER     = C @(0x7528, 0x6237, 0x804A, 0x5929, 0x5BA4)                                     # yong-hu-liao-tian-shi
$GREETING   = C @(0x6B22, 0x8FCE, 0x4F7F, 0x7528, 0x20, 0x6C, 0x61, 0x6E, 0x77)                # huan-ying-shi-yong " lanw"
$INPUT_NAME = C @(0x804A, 0x5929, 0x8F93, 0x5165, 0x6846)                                     # liao-tian-shu-ru-kuang
$SEND       = C @(0x53D1, 0x9001)                                                             # fa-song
$ENTER      = C @(0x8FDB, 0x5165, 0x804A, 0x5929, 0x5BA4)                                     # jin-ru-liao-tian-shi
$TOGGLE     = C @(0x5F00, 0x542F, 0x804A, 0x5929, 0x5BA4) + [char]0xFF08 + 'chatEnable' + [char]0xFF09
$ERR_SEND   = C @(0x5C1A, 0x672A, 0x8FDE, 0x63A5, 0x804A, 0x5929, 0x5BA4, 0x670D, 0x52A1, 0x5668) # shang-wei-lian-jie-liao-tian-shi-fu-wu-qi
$CONN_FAIL  = C @(0x8FDE, 0x63A5, 0x5931, 0x8D25)                                             # lian-jie-shi-bai
$CONN_OK    = C @(0x5DF2, 0x8FDE, 0x63A5)                                                     # yi-lian-jie
$SECTION    = [string][char]0x00A7                                                            # section sign
$MSG_TEXT   = "VERIFY27 hello from fe-3"

function Stop-Lanw {
    Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
}

function Get-Root([IntPtr]$hwnd) { return [System.Windows.Automation.AutomationElement]::FromHandle($hwnd) }

function Find-Exact([IntPtr]$hwnd, [string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return (Get-Root $hwnd).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Find-Contains([IntPtr]$hwnd, [string]$fragment) {
    $all = (Get-Root $hwnd).FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) { $n = $e.Current.Name; if ($n -and $n.Contains($fragment)) { return $e } }
    return $null
}

function Wait-Exact([IntPtr]$hwnd, [string]$name, [int]$timeoutSec = 20) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $hit = Find-Exact $hwnd $name
        if ($null -ne $hit) { return $hit }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Wait-Contains([IntPtr]$hwnd, [string]$fragment, [int]$timeoutSec = 20) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $hit = Find-Contains $hwnd $fragment
        if ($null -ne $hit) { return $hit }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Start-Lanw {
    $proc = Start-Process -FilePath $ExePath -PassThru
    $hwnd = Get-LanwWindowHandle -ProcessId $proc.Id -TimeoutSeconds 45
    if ($hwnd -eq [IntPtr]::Zero) { throw "Lanw.App has no main window" }
    Start-Sleep -Seconds 3
    return @{ Proc = $proc; Hwnd = $hwnd }
}

# ---------------------------------------------------------------- run
Stop-Lanw
$result = [ordered]@{
    PageOpened    = $false
    ColorStripped = $false
    ToggleWorks   = $false
    SendError     = $false
    ConnectState  = ''
    NoLiteralBind = $false
    Alive         = $false
}

$app = $null
try {
    $app = Start-Lanw
    $proc = $app.Proc
    $hwnd = $app.Hwnd
    Write-Host "window handle: $hwnd"

    Write-Host "[1] open ChatPage via the nav item (helper walks up to the clickable ListItem)"
    $how = $null
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $how = Invoke-LanwByName -Root (Get-Root $hwnd) -Name $NAV_CHAT
        Write-Host "      nav invoke: $how"
        if ($null -ne (Wait-Contains $hwnd $HEADER 8)) { break }
        Write-Host "      attempt ${attempt}: chat page not rendered yet"
    }

    # self-proof of the page switch: the header AND a control that only ChatPage has
    $header = Wait-Contains $hwnd $HEADER 8
    $input = Wait-Exact $hwnd $INPUT_NAME 8
    $result.PageOpened = ($null -ne $header) -and ($null -ne $input)
    Write-Host "      header found: $($null -ne $header); chat input found: $($null -ne $input)"
    if (-not $result.PageOpened) { throw "ChatPage not reached (header/input missing)" }

    Write-Host "[2] welcome bubble rendered; section-sign color codes stripped by MinecraftColorCodeConverter"
    $greeting = Wait-Contains $hwnd $GREETING 10
    if ($null -ne $greeting) {
        $name = $greeting.Current.Name
        Write-Host "      bubble text: $name"
        $result.ColorStripped = -not $name.Contains($SECTION)
    } else {
        Write-Host "      welcome bubble not found"
    }

    Write-Host "[3] chatEnable toggle (toggled twice to restore the original value)"
    $toggle = Wait-Exact $hwnd $TOGGLE 8
    if ($null -eq $toggle) { throw "toggle switch '$TOGGLE' not found" }
    $pattern = $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $state0 = $pattern.Current.ToggleState.ToString()
    $pattern.Toggle()
    Start-Sleep -Milliseconds 1500
    $state1 = $pattern.Current.ToggleState.ToString()
    $pattern.Toggle()
    Start-Sleep -Milliseconds 1500
    $state2 = $pattern.Current.ToggleState.ToString()
    Write-Host "      toggle states: $state0 -> $state1 -> $state2"
    $result.ToggleWorks = ($state0 -ne $state1) -and ($state1 -ne $state2) -and ($state0 -eq $state2)

    Write-Host "[4] send while not connected -> error bubble, no crash"
    $value = $input.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $value.SetValue($MSG_TEXT)
    Start-Sleep -Milliseconds 700
    $sendHow = Invoke-LanwByName -Root (Get-Root $hwnd) -Name $SEND
    Write-Host "      send invoke: $sendHow"
    $err = Wait-Contains $hwnd $ERR_SEND 12
    $result.SendError = ($null -ne $err)
    $proc.Refresh()
    Write-Host "      error bubble found: $($result.SendError); alive: $(-not $proc.HasExited)"

    Write-Host "[5] render sanity: no literal {x:Bind ...} leaked into the UI"
    $lit = Test-LanwNoLiteralBinding -Root (Get-Root $hwnd)
    $result.NoLiteralBind = $lit.Pass
    Write-Host "      no literal binding leaks: $($lit.Pass)"

    Write-Host "[6] try to enter the chat room (network may be unavailable - must not crash)"
    $enterHow = Invoke-LanwByName -Root (Get-Root $hwnd) -Name $ENTER
    Write-Host "      enter invoke: $enterHow"
    if ($null -ne $enterHow) {
        $deadline = (Get-Date).AddSeconds(45)
        while ((Get-Date) -lt $deadline -and $result.ConnectState -eq '') {
            if ($null -ne (Find-Contains $hwnd $CONN_FAIL)) { $result.ConnectState = 'failed' }
            elseif ($null -ne (Find-Contains $hwnd $CONN_OK)) { $result.ConnectState = 'connected' }
            else { Start-Sleep -Milliseconds 900 }
        }
        if ($result.ConnectState -eq '') { $result.ConnectState = 'still-connecting' }
    } else {
        $result.ConnectState = 'enter-not-found'
    }
    Write-Host "      connect state: $($result.ConnectState)"

    Save-LanwScreenshot -WindowHandle $hwnd -Path $ShotPath | Out-Null
    Write-Host "      shot: $ShotPath"
    $proc.Refresh()
    $result.Alive = -not $proc.HasExited
} finally {
    Stop-Lanw
}

Write-Host ""
$result.GetEnumerator() | ForEach-Object { "{0} = {1}" -f $_.Key, $_.Value } | Write-Host

$connectOk = $result.ConnectState -in @('failed', 'connected', 'still-connecting')
$ok = $result.PageOpened -and $result.ColorStripped -and $result.ToggleWorks -and $result.SendError -and
      $result.NoLiteralBind -and $connectOk -and $result.Alive
if ($ok) {
    Write-Host "PASS: ChatPage opens; chatEnable switch toggles both ways; send-while-disconnected shows the error state; color codes stripped; process alive"
    exit 0
} else {
    Write-Host "FAIL: see values above"
    exit 1
}
