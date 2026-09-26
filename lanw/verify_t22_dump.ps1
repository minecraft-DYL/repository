# t22 diagnostic dump: navigate to Home and print every accessible text (ASCII-only source).
param(
    [string]$ExeDir = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64",
    [string]$ExeName = "Lanw.App.exe"
)
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$codes) { return (-join ($codes | ForEach-Object { [char]$_ })) }
$N_NAV = S @(0x4E3B, 0x9875)
$ExePath = Join-Path $ExeDir $ExeName
$ProcName = [System.IO.Path]::GetFileNameWithoutExtension($ExeName)
Get-Process $ProcName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800
$proc = Start-Process -FilePath $ExePath -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $proc.Id -TimeoutSeconds 25
[void][LanwUiaWin32]::ShowWindow($hwnd, 3)
Start-Sleep -Milliseconds 2000
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
$how = Invoke-LanwByName -Root $root -Name $N_NAV
Start-Sleep -Milliseconds 2500
Write-Host "[dump] nav=$how hwnd=$hwnd"
Get-LanwTexts -Root $root | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
$proc.Refresh()
Write-Host "[dump] alive=$(-not $proc.HasExited)"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
