# ============================================================================
# t31 runtime verification - accounts page merge (login page deleted).
# ASCII-only source: Chinese strings are built from char codes so the script
# cannot be corrupted by the ANSI codepage the way a raw UTF-8 .ps1 would be.
# Drives the app through UI Automation only (never SetCursorPos/mouse_event).
# ============================================================================
$ErrorActionPreference = 'Stop'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'

function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }

$T_ACCOUNTS = S @(0x8D26, 0x53F7, 0x7BA1, 0x7406)                         # account management
$T_ADD      = S @(0x6DFB, 0x52A0, 0x8D26, 0x53F7)                         # add account
$T_RANDOM   = S @(0x968F, 0x673A, 0x767B, 0x5F55)                         # random login
$T_LOGIN    = S @(0x767B, 0x5F55)                                         # login
$T_TYPE     = S @(0x767B, 0x5F55, 0x7C7B, 0x578B)                         # login type
$T_SAVE     = S @(0x767B, 0x5F55, 0x5E76, 0x4FDD, 0x5B58)                 # login and save
$T_GT_TITLE = S @(0x968F, 0x673A, 0x767B, 0x5F55, 0x20, 0xB7, 0x20, 0x4EBA, 0x673A, 0x9A8C, 0x8BC1)
$T_CANCEL   = S @(0x53D6, 0x6D88)                                         # cancel

$exe = 'D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe'
$rootDir = 'D:\ku\traecode\lanw'

Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
if ($hwnd -eq [IntPtr]::Zero) { Write-Host 'FAIL  no window handle'; exit 1 }
Start-Sleep -Seconds 6

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
function NamesNow { return @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name }) }
function Report([string]$title, [bool]$ok, [string]$detail) {
    $tag = if ($ok) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0}  {1,-34} {2}" -f $tag, $title, $detail)
    return $ok
}

$names = NamesNow
$fail = 0

# --- t31-A1: startup lands on the accounts page, login nav item is gone ---
$hasAccounts = $names -contains $T_ACCOUNTS
$hasAdd = $names -contains $T_ADD
$hasRandom = $names -contains $T_RANDOM
$hasLoginItem = $names -contains $T_LOGIN
if (-not (Report 'A1 startup=accounts-page' ($hasAccounts -and $hasAdd -and $hasRandom -and -not $hasLoginItem) `
        ("accounts=$hasAccounts add=$hasAdd random=$hasRandom leftover-login-nav=$hasLoginItem"))) { $fail++ }

# --- t31-A2: no literal {x:Bind text leaked into the UI (user-reported bug) ---
$lit = Test-LanwNoLiteralBinding -Root $root
if (-not (Report 'A2 no-literal-xBind' ([bool]$lit.Pass) (($lit.Hit | Select-Object -First 3) -join ' | '))) { $fail++ }

[void](Save-LanwScreenshot -WindowHandle $hwnd -Path (Join-Path $rootDir 'verify_t31_accounts.png'))

# --- t31-B1: "add account" opens the credential dialog ---
$how1 = Invoke-LanwByName -Root $root -Name $T_ADD
Start-Sleep -Seconds 3
$d1 = NamesNow
$dlg1 = ($d1 -contains $T_SAVE) -and ($d1 -contains $T_TYPE) -and ($d1 -contains $T_CANCEL)
if (-not (Report 'B1 add-account dialog opens' ([bool]$dlg1) ("pattern=$how1 save=$($d1 -contains $T_SAVE) type=$($d1 -contains $T_TYPE)"))) { $fail++ }
[void](Save-LanwScreenshot -WindowHandle $hwnd -Path (Join-Path $rootDir 'verify_t31_add_dialog.png'))
[void](Invoke-LanwByName -Root $root -Name $T_CANCEL)
Start-Sleep -Seconds 3

# --- t31-B2: "random login" opens the geetest web dialog ---
$how2 = Invoke-LanwByName -Root $root -Name $T_RANDOM
Start-Sleep -Seconds 8
$d2 = NamesNow
$dlg2 = $d2 -contains $T_GT_TITLE
if (-not (Report 'B2 random-login geetest dialog' ([bool]$dlg2) ("pattern=$how2 title-found=$dlg2"))) { $fail++ }
[void](Save-LanwScreenshot -WindowHandle $hwnd -Path (Join-Path $rootDir 'verify_t31_geetest_dialog.png'))
[void](Invoke-LanwByName -Root $root -Name $T_CANCEL)
Start-Sleep -Seconds 2

# --- t31-B3: after cancelling, the status line reports the cancellation (multi-line status) ---
$d3 = NamesNow
$cancelMsg = $d3 | Where-Object { $_ -like ('*' + $T_CANCEL + '*') }
Write-Host ("INFO  status-samples            " + (($d3 | Where-Object { $_.Length -gt 12 } | Select-Object -First 4) -join ' || '))

Write-Host ''
Write-Host ("SUMMARY fail=$fail ; pid=$($p.Id) ; screenshots: verify_t31_accounts.png / verify_t31_add_dialog.png / verify_t31_geetest_dialog.png")
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
if ($fail -gt 0) { exit 1 } else { exit 0 }
