# ============================================================================
# t33 verify: per-row "edit account" dialog (edit -> validate -> save -> persist)
#
# The real account.json may be empty, so this seeds a throwaway 4399 account,
# exercises the whole edit path (dialog prefill -> change name -> save ->
# write back to account.json), then restores the original file byte-for-byte.
# The user's real account data is never left modified.
# ============================================================================
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }

$T_MANAGE = S @(0x8D26, 0x53F7, 0x7BA1, 0x7406)                 # accounts page nav
$T_EDIT = S @(0x7F16, 0x8F91)                                   # edit
$T_EDITDLG = S @(0x7F16, 0x8F91, 0x8D26, 0x53F7)                # edit-account dialog title
$T_NAME = S @(0x540D, 0x79F0)                                   # name
$T_TYPE = S @(0x8D26, 0x53F7, 0x7C7B, 0x578B)                   # account type
$T_ACCOUNT = S @(0x8D26, 0x53F7)                                # account
$T_PASSWORD = S @(0x5BC6, 0x7801)                               # password
$T_SAVE = S @(0x4FDD, 0x5B58)                                   # save
$T_SAVED = S @(0x5DF2, 0x4FDD, 0x5B58, 0x8D26, 0x53F7)          # saved-account status prefix
$T_BASIC = S @(0x8BF7, 0x586B, 0x5199, 0x6240, 0x6709, 0x57FA, 0x672C, 0x4FE1, 0x606F)  # please fill in all basic info

$SEED_NAME = 'lanw-edit-seed'
$NEW_NAME = 'lanw-edit-saved'
$fail = 0

function Set-LanwValue {
    param($Root, [string]$Name, [string]$Value)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $el = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $el) { return $false }
    $obj = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$obj)) { $obj.SetValue($Value); return $true }
    return $false
}

function Get-LanwValue {
    param($Root, [string]$Name)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $el = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $el) { return $null }
    $obj = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$obj)) { return $obj.Current.Value }
    return $null
}

# --- locate the running output dir (the build may emit to bin\x64\Debug\...) ---
$cand = Get-ChildItem -Path 'D:\ku\traecode\lanw\src\App\Lanw.App\bin' -Recurse -Filter 'Lanw.App.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $cand) { Write-Host 'FAIL  cannot find Lanw.App.exe'; exit 1 }
$exe = $cand.FullName
$exeDir = $cand.DirectoryName
$accountFile = Join-Path $exeDir 'resources\account.json'
$backupFile = "$accountFile.captain-bak"
Write-Host ("INFO  exe       = " + $exe)
Write-Host ("INFO  account   = " + $accountFile)

# --- seed a throwaway account, preserving the original file ---
$hadOriginal = Test-Path $accountFile
if ($hadOriginal) { Copy-Item $accountFile $backupFile -Force }
New-Item -ItemType Directory -Force -Path (Split-Path $accountFile) | Out-Null
$seed = '[{"name":"' + $SEED_NAME + '","account":"lanw_edit_test","type":"4399","password":"pw123456","id":0}]'
Set-Content -Path $accountFile -Value $seed -Encoding UTF8
Write-Host ("INFO  seeded 1 throwaway 4399 account (original backed up: " + $hadOriginal + ")")

try {
    Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    $p = Start-Process $exe -PassThru
    $hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
    Start-Sleep -Seconds 6
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    [void](Invoke-LanwByName -Root $root -Name $T_MANAGE)
    Start-Sleep -Seconds 3

    $texts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    $seeded = $texts -contains $SEED_NAME
    if (-not $seeded) { $fail++ }
    Write-Host ("{0}  {1,-28} (seeded row visible={2})" -f $(if ($seeded) { 'PASS' } else { 'FAIL' }), 'row from account.json', $seeded)

    # --- open the edit dialog ---
    $how = Invoke-LanwByName -Root $root -Name $T_EDIT
    Start-Sleep -Seconds 3
    $dtexts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    $dlgOk = ($dtexts -contains $T_EDITDLG) -and ($dtexts -contains $T_NAME) -and ($dtexts -contains $T_TYPE) -and ($dtexts -contains $T_ACCOUNT) -and ($dtexts -contains $T_PASSWORD) -and ($dtexts -contains $T_SAVE)
    if (-not $dlgOk) { $fail++ }
    Write-Host ("{0}  {1,-28} (invoked via {2}; fields 名称/账号类型/账号/密码 + 保存 = {3})" -f $(if ($dlgOk) { 'PASS' } else { 'FAIL' }), 'edit dialog opens', $how, $dlgOk)

    $prefill = Get-LanwValue -Root $root -Name $T_NAME
    $preOk = $prefill -eq $SEED_NAME
    if (-not $preOk) { $fail++ }
    Write-Host ("{0}  {1,-28} (name prefill='{2}')" -f $(if ($preOk) { 'PASS' } else { 'FAIL' }), 'prefilled from row', $prefill)

    # --- validation leg: empty name must be rejected and keep the dialog open ---
    [void](Set-LanwValue -Root $root -Name $T_NAME -Value '')
    [void](Invoke-LanwByName -Root $root -Name $T_SAVE)
    Start-Sleep -Seconds 2
    $vtexts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    $stillOpen = $vtexts -contains $T_EDITDLG
    $warned = @($vtexts | Where-Object { $_.Contains($T_BASIC) }).Count -gt 0
    $valOk = $stillOpen -and $warned
    if (-not $valOk) { $fail++ }
    Write-Host ("{0}  {1,-28} (dialog stays open={2}, shows 请填写所有基本信息={3})" -f $(if ($valOk) { 'PASS' } else { 'FAIL' }), 'validation rejects empty name', $stillOpen, $warned)

    # --- save leg ---
    [void](Set-LanwValue -Root $root -Name $T_NAME -Value $NEW_NAME)
    [void](Invoke-LanwByName -Root $root -Name $T_SAVE)

    $status = ''
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        $now = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
        $hit = @($now | Where-Object { $_.StartsWith($T_SAVED) })
        if ($hit.Count -gt 0) { $status = $hit -join ' | '; break }
    }

    $alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
    $saveOk = $alive -and $status -ne ''
    if (-not $saveOk) { $fail++ }
    Write-Host ("{0}  {1,-28} alive={2} status='{3}'" -f $(if ($saveOk) { 'PASS' } else { 'FAIL' }), 'save reports success', $alive, $status)

    Start-Sleep -Seconds 1
    $listTexts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    $rowUpdated = $listTexts -contains $NEW_NAME
    if (-not $rowUpdated) { $fail++ }
    Write-Host ("{0}  {1,-28} (list shows new name={2})" -f $(if ($rowUpdated) { 'PASS' } else { 'FAIL' }), 'row re-rendered', $rowUpdated)

    Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2

    # --- the decisive evidence: did it actually land in account.json? ---
    $json = if (Test-Path $accountFile) { Get-Content $accountFile -Raw } else { '' }
    $persisted = $json.Contains($NEW_NAME) -and $json.Contains('lanw_edit_test')
    if (-not $persisted) { $fail++ }
    Write-Host ("{0}  {1,-28} (account.json contains new name={2})" -f $(if ($persisted) { 'PASS' } else { 'FAIL' }), 'persisted to account.json', $json.Contains($NEW_NAME))
}
finally {
    # --- restore the user's original data, always ---
    if ($hadOriginal) { Copy-Item $backupFile $accountFile -Force; Remove-Item $backupFile -Force -ErrorAction SilentlyContinue }
    else { Remove-Item $accountFile -Force -ErrorAction SilentlyContinue }
    Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
    $restored = if ($hadOriginal) { (Get-Content $accountFile -Raw).Length } else { 0 }
    Write-Host ("INFO  account.json restored (orig had file=" + $hadOriginal + ", bytes=" + $restored + ")")
}

Write-Host ""
Write-Host "SUMMARY t33-account-edit fail=$fail"
if ($fail -eq 0) { Write-Host 'RESULT: PASS' } else { Write-Host 'RESULT: FAIL' }
