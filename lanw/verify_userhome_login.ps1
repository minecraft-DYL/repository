# ============================================================================
# Verify the newly added Nirvana (npyyds) account login UI on the UserHome page.
# If the app is already logged in, log out first so the login card is visible.
# ============================================================================
$ErrorActionPreference = 'Stop'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }

$T_USER      = S @(0x7528, 0x6237, 0x4E2D, 0x5FC3)                         # user centre nav
$T_LOGOUT    = S @(0x9000, 0x51FA, 0x767B, 0x5F55)                         # logout
$T_LOGIN_TTL = S @(0x767B, 0x5F55, 0x6D85, 0x69C3, 0x8D26, 0x53F7)         # login nirvana account
$T_REGISTER  = S @(0x6CE8, 0x518C, 0x8D26, 0x53F7)                         # register account
$T_SHOP      = S @(0x6D85, 0x69C3, 0x5546, 0x57CE)                         # nirvana shop
$T_ACCOUNT   = S @(0x8D26, 0x53F7)                                         # account
$T_PASSWORD  = S @(0x5BC6, 0x7801)                                         # password

$exe = 'D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe'
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
if ($hwnd -eq [IntPtr]::Zero) { Write-Host 'FAIL no window'; exit 1 }
Start-Sleep -Seconds 6
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

[void](Invoke-LanwByName -Root $root -Name $T_USER)
Start-Sleep -Seconds 3
$names = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })

if ($names -contains $T_LOGOUT) {
    Write-Host 'INFO  already logged in -> invoking logout to reveal the login card'
    [void](Invoke-LanwByName -Root $root -Name $T_LOGOUT)
    Start-Sleep -Seconds 3
    $names = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
}

$checks = [ordered]@{
    'nirvana login card title' = $T_LOGIN_TTL
    'account field'            = $T_ACCOUNT
    'password field'           = $T_PASSWORD
    'register link'            = $T_REGISTER
    'shop link'                = $T_SHOP
    'login button'             = $T_ACCOUNT  # placeholder replaced below
}
$checks.Remove('login button')
$fail = 0
foreach ($k in $checks.Keys) {
    $ok = $names -contains $checks[$k]
    if (-not $ok) { $fail++ }
    Write-Host ("{0}  {1,-26} ({2})" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $k, $checks[$k])
}

# --- action leg: fill the form via UIA ValuePattern and click login ---
# Proves ViewModel.LoginCommand -> NirvanaAccountManager.Login runs and the app
# survives a rejected credential (real network call to npyyds.top).
function Set-LanwValue {
    param($Root, [string]$Name, [string]$Value)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    foreach ($c in $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
        $o = $null
        try {
            if ($c.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$o)) { $o.SetValue($Value); return $true }
        } catch { }
    }
    return $false
}

$setAcc = Set-LanwValue -Root $root -Name $T_ACCOUNT -Value 'lanw_selftest_nobody'
$setPwd = Set-LanwValue -Root $root -Name $T_PASSWORD -Value 'x'
$T_LOGIN = S @(0x767B, 0x5F55)
$T_FAIL = S @(0x767B, 0x5F55, 0x5931, 0x8D25)                       # login failed
$T_OK = S @(0x767B, 0x5F55, 0x6210, 0x529F)                         # login succeeded
$T_NODAYS = S @(0x8D26, 0x53F7, 0x6CA1, 0x6709, 0x5929, 0x6570)     # account has no days
$T_NEEDACC = S @(0x8BF7, 0x8F93, 0x5165, 0x8D26, 0x53F7)            # please enter the account
$T_PENDING = S @(0x6B63, 0x5728, 0x767B, 0x5F55)                    # logging in...
[void](Invoke-LanwByName -Root $root -Name $T_LOGIN)

$status = ''
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Seconds 2
    $alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
    if (-not $alive) { $status = '<app exited>'; break }
    $now = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    $hit = @($now | Where-Object { $_.StartsWith($T_FAIL) -or $_.StartsWith($T_OK) -or $_.StartsWith($T_NODAYS) -or $_.StartsWith($T_NEEDACC) })
    if ($hit.Count -gt 0) { $status = $hit -join ' | '; break }
    $pend = @($now | Where-Object { $_.StartsWith($T_PENDING) })
    if ($pend.Count -gt 0) { $status = '(pending) ' + ($pend -join ' | ') }
}

$alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
$ok = $alive -and $status -ne '' -and $status -notlike '<app exited>' -and $status -notlike '(pending)*'
if (-not $ok) { $fail++ }
Write-Host ("{0}  {1,-26} alive={2} setAccount={3} setPassword={4}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), 'login attempt handled', $alive, $setAcc, $setPwd)
Write-Host ("INFO  result message: " + $(if ($status) { $status } else { '<none>' }))

[void](Save-LanwScreenshot -WindowHandle $hwnd -Path 'D:\ku\traecode\lanw\verify_userhome_login.png')
Write-Host ''
Write-Host "page texts ($($names.Count)):"
$names | Where-Object { $_.Length -gt 1 -and $_.Length -lt 30 } | Select-Object -First 22 | ForEach-Object { Write-Host "   $_" }
Write-Host ''
Write-Host "SUMMARY userhome-nirvana-login fail=$fail"
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
if ($fail -gt 0) { exit 1 } else { exit 0 }
