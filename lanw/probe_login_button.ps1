# ============================================================================
# Probe: why did clicking the UserHome "login" button do nothing?
# Dumps every element (type / name / enabled) and reports what the button
# exposes, then invokes it and watches the status text.
# ============================================================================
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }
$T_USER = S @(0x7528, 0x6237, 0x4E2D, 0x5FC3)
$T_LOGOUT = S @(0x9000, 0x51FA, 0x767B, 0x5F55)
$T_LOGIN = S @(0x767B, 0x5F55)

$exe = 'D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe'
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
Start-Sleep -Seconds 6
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
[void](Invoke-LanwByName -Root $root -Name $T_USER)
Start-Sleep -Seconds 3
$names = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
if ($names -contains $T_LOGOUT) { [void](Invoke-LanwByName -Root $root -Name $T_LOGOUT); Start-Sleep -Seconds 3 }

Write-Host '--- all elements (type | enabled | name) ---'
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $all) {
    $ct = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
    Write-Host ("{0,-16} {1,-6} {2}" -f $ct, $e.Current.IsEnabled, $e.Current.Name)
}

Write-Host ''
Write-Host "--- the login button specifically ---"
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $T_LOGIN)
$cands = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
Write-Host "candidates named '$T_LOGIN': $($cands.Count)"
foreach ($c in $cands) {
    $o = $null
    $pats = @()
    if ($c.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$o)) { $pats += 'InvokePattern' }
    $o = $null
    if ($c.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$o)) { $pats += 'ValuePattern' }
    $o = $null
    if ($c.TryGetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern, [ref]$o)) { $pats += 'LegacyIAccessible' }
    Write-Host ("  type={0} enabled={1} offscreen={2} patterns=[{3}]" -f $c.Current.ControlType.ProgrammaticName, $c.Current.IsEnabled, $c.Current.IsOffscreen, ($pats -join ','))
}

$how = Invoke-LanwByName -Root $root -Name $T_LOGIN
Write-Host "Invoke-LanwByName returned: $how"
Start-Sleep -Seconds 3
Write-Host '--- status line candidates right after the click ---'
$post = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $post) {
    $n = $e.Current.Name
    if ($n -and $n.Length -gt 12) { Write-Host ("  [{0}] {1}" -f $e.Current.ControlType.ProgrammaticName, $n) }
}

Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
