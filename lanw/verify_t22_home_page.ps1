# t22 runtime verification (ASCII-only source; Chinese UI names built from char codes via S()).
# NO SetCursorPos / mouse_event / SendKeys: all driving goes through tools\UiaHelper.ps1
# (UIA patterns first, PostMessage-to-window only as a last resort).
param(
    [string]$ExeDir = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64",
    [string]$ExeName = "Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_t22_home_page.png"
)
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'

$ExePath = Join-Path $ExeDir $ExeName
$ProcName = [System.IO.Path]::GetFileNameWithoutExtension($ExeName)

function S([int[]]$codes) { return (-join ($codes | ForEach-Object { [char]$_ })) }
$N_NAV      = S @(0x4E3B, 0x9875)                                                              # home nav item
$N_TITLE    = (S @(0x6B22, 0x8FCE, 0x4F7F, 0x7528)) + " Lanw " + (S @(0x542F, 0x52A8, 0x5668)) # welcome heading
$N_ADS      = S @(0x516C, 0x544A)                                                              # ads heading
$N_VERCARD  = S @(0x7248, 0x672C, 0x4FE1, 0x606F)                                              # version-info card title
$N_GAMEVER  = S @(0x6E38, 0x620F, 0x7248, 0x672C)                                              # game version label
$N_CRC      = "CRC " + (S @(0x76D0, 0x503C))                                                   # crc salt label
$N_RANDCARD = S @(0x968F, 0x673A, 0x540D, 0x751F, 0x6210, 0x5668)                              # random-name card title
$N_GEN      = S @(0x751F, 0x6210, 0x968F, 0x673A, 0x540D)                                      # generate button
$N_COPY     = S @(0x590D, 0x5236)                                                              # copy button
$N_ADSLOT   = S @(0x5E7F, 0x544A, 0x4F4D)                                                      # ad placeholder title prefix
$K_DE       = S @(0x7684)                                                                      # "de" inside generated names
$K_PLACE    = S @(0x70B9, 0x51FB, 0x4E0B, 0x65B9, 0x6309, 0x94AE, 0x751F, 0x6210)              # placeholder text
$K_LOADING  = S @(0x83B7, 0x53D6, 0x4E2D)                                                      # fetching
$K_FAILED   = S @(0x83B7, 0x53D6, 0x5931, 0x8D25)                                              # fetch failed
$K_NOSALT   = S @(0x672A, 0x4E0B, 0x53D1)                                                      # salt not issued
$K_LOADERR  = S @(0x52A0, 0x8F7D, 0x5931, 0x8D25)                                              # load failed
$K_UPDATED  = S @(0x5DF2, 0x66F4, 0x65B0)                                                      # updated
$K_NONET    = S @(0x65E0, 0x6CD5, 0x8FDE, 0x63A5)                                              # cannot connect

$AE = [System.Windows.Automation.AutomationElement]

$proc = $null; $hwnd = [IntPtr]::Zero; $root = $null
for ($attempt = 1; $attempt -le 3; $attempt++) {
    Get-Process $ProcName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    $proc = Start-Process -FilePath $ExePath -PassThru
    $hwnd = Get-LanwWindowHandle -ProcessId $proc.Id -TimeoutSeconds 25
    $proc.Refresh()
    Write-Host "[1] attempt $attempt : pid $($proc.Id) exited=$($proc.HasExited) hwnd=$hwnd"
    if (-not $proc.HasExited -and $hwnd -ne [IntPtr]::Zero) { break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "FAIL: no live app window"; exit 2 }
[void][LanwUiaWin32]::ShowWindow($hwnd, 3)
Start-Sleep -Milliseconds 1500
$bigger = [LanwUiaWin32]::FindLargest([uint32]$proc.Id)
if ($bigger -ne [IntPtr]::Zero) { $hwnd = $bigger }
$root = $AE::FromHandle($hwnd)
Write-Host "[1] window handle: $hwnd"

function Text-List { return @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name }) }
function Has-Text([string]$needle) { foreach ($n in (Text-List)) { if ($n -and $n.Contains($needle)) { return $true } } return $false }
function Find-Text([string]$needle) { foreach ($n in (Text-List)) { if ($n -and $n.Contains($needle)) { return $n } } return $null }

# ---- 1) navigate to Home, assert page-specific markers (retry: another script may steal the page)
$title = $false; $ads = $false; $verCard = $false; $gameVer = $false; $crc = $false; $randCard = $false; $genBtn = $false
for ($r = 1; $r -le 4; $r++) {
    $how = Invoke-LanwByName -Root $root -Name $N_NAV
    Start-Sleep -Milliseconds 1500
    $title = Has-Text $N_TITLE
    $ads = Has-Text $N_ADS
    $verCard = Has-Text $N_VERCARD
    $gameVer = Has-Text $N_GAMEVER
    $crc = Has-Text $N_CRC
    $randCard = Has-Text $N_RANDCARD
    $genBtn = Has-Text $N_GEN
    Write-Host "[2] round $r : nav=$how title=$title ads=$ads verCard=$verCard gameVer=$gameVer crc=$crc randCard=$randCard genBtn=$genBtn"
    if ($title -and $ads -and $verCard -and $gameVer -and $crc -and $randCard -and $genBtn) { break }
}
$adSlot = Has-Text $N_ADSLOT
$copyBtn = Has-Text $N_COPY
$verValue = Find-Text $K_LOADING
if ($null -eq $verValue) { $verValue = Find-Text $K_FAILED }
$saltValue = Find-Text $K_NOSALT
$statusValue = Find-Text $K_UPDATED
if ($null -eq $statusValue) { $statusValue = Find-Text $K_NONET }
if ($null -eq $statusValue) { $statusValue = Find-Text $K_LOADERR }
Write-Host "[3] ad placeholder title: $adSlot ; copy button: $copyBtn"
Write-Host "[3] game version value: '$verValue'"
Write-Host "[3] crc salt value: '$saltValue'"
Write-Host "[3] status text: '$statusValue'"

# ---- 2) random-name generation via the UIA InvokePattern (no cursor movement)
$before = Find-Text $K_PLACE
Write-Host "[4] placeholder before: '$before'"
$genHow = Invoke-LanwByName -Root $root -Name $N_GEN
Write-Host "[4] generate invoked via: $genHow"
Start-Sleep -Milliseconds 1200
$generated = $null
foreach ($n in (Text-List)) {
    if ($n -and $n.Length -ge 7 -and $n.Length -le 9 -and $n.Contains($K_DE) -and $n -ne $before) { $generated = $n; break }
}
if ($null -eq $generated) {
    [void](Invoke-LanwByName -Root $root -Name $N_NAV)
    Start-Sleep -Milliseconds 1200
    [void](Invoke-LanwByName -Root $root -Name $N_GEN)
    Start-Sleep -Milliseconds 1200
    foreach ($n in (Text-List)) {
        if ($n -and $n.Length -ge 7 -and $n.Length -le 9 -and $n.Contains($K_DE) -and $n -ne $before) { $generated = $n; break }
    }
}
Write-Host "[4] generated random name: '$generated'"
if ($null -eq $generated) {
    Write-Host "--- all texts (debug) ---"
    Get-LanwTexts -Root $root | Format-Table -AutoSize | Out-String | Write-Host
}

# ---- 3) literal-binding guard + screenshot
$lit = Test-LanwNoLiteralBinding -Root $root
Write-Host "[5] literal x:Bind leakage: Pass=$($lit.Pass) Hits=$($lit.Hit -join ' | ')"
$shot = Save-LanwScreenshot -WindowHandle $hwnd -Path $ShotPath
$rect = New-Object LanwUiaWin32+RECT
[void][LanwUiaWin32]::GetWindowRect($hwnd, [ref]$rect)
Write-Host "[6] screenshot: $shot ($($rect.Right - $rect.Left)x$($rect.Bottom - $rect.Top))"

$proc.Refresh()
$alive = -not $proc.HasExited
Write-Host "[7] alive at end: $alive"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

$ok = $alive -and $title -and $ads -and $verCard -and $gameVer -and $crc -and $randCard -and $genBtn -and $lit.Pass -and ($null -ne $generated)
Write-Host ""
if ($ok) { Write-Host "PASS: Home page shows welcome/ads/version-info/CRC/random-name cards, generates a 7-9 char name, no literal binding"; exit 0 }
Write-Host ("FAIL: alive={0} title={1} ads={2} verCard={3} gameVer={4} crc={5} randCard={6} genBtn={7} noLiteral={8} generated={9}" -f $alive, $title, $ads, $verCard, $gameVer, $crc, $randCard, $genBtn, $lit.Pass, ($null -ne $generated))
exit 1
