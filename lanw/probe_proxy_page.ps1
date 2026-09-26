# ============================================================================
# Probe: Proxy page rendered 0 elements in the nav sweep. Is the page broken,
# or did the sweep just measure before it finished rendering?
# ============================================================================
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }
$T_PROXY = S @(0x4EE3, 0x7406)   # proxy

$cand = Get-ChildItem -Path 'D:\ku\traecode\lanw\src\App\Lanw.App\bin' -Recurse -Filter 'Lanw.App.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$exe = $cand.FullName
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $exe -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
Start-Sleep -Seconds 7
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

$how = Invoke-LanwByName -Root $root -Name $T_PROXY
Write-Host ("nav invoked via: " + $how)

for ($wait = 2; $wait -le 10; $wait += 2) {
    Start-Sleep -Seconds 2
    $texts = @(Get-LanwTexts -Root $root | ForEach-Object { $_.Name })
    $alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
    Write-Host ("t+{0,2}s  alive={1}  elements={2}" -f $wait, $alive, $texts.Count)
    if ($texts.Count -gt 3) {
        Write-Host "--- visible texts ---"
        $texts | Where-Object { $_.Length -gt 1 -and $_.Length -lt 60 } | Select-Object -First 25 | ForEach-Object { "    " + $_ }
        break
    }
}

[void](Save-LanwScreenshot -WindowHandle $hwnd -Path 'D:\ku\traecode\lanw\probe_proxy_page.png')
$alive = $null -ne (Get-Process -Id $p.Id -ErrorAction SilentlyContinue)
Write-Host ("alive at end: " + $alive)
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
