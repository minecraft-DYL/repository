# Read the Geetest dialog state from BOTH the window root and the UIA desktop root
# (the ContentDialog + its status TextBlock may live outside the window subtree).
$ErrorActionPreference = 'Continue'
. 'D:\ku\traecode\lanw\tools\UiaHelper.ps1'
function S([int[]]$c) { -join ($c | ForEach-Object { [char]$_ }) }
$T_ACCOUNTS = S @(0x8D26, 0x53F7, 0x7BA1, 0x7406)
$T_RANDOM = S @(0x968F, 0x673A, 0x767B, 0x5F55)

$cand = Get-ChildItem -Path 'D:\ku\traecode\lanw\src\App\Lanw.App\bin' -Recurse -Filter 'Lanw.App.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$p = Start-Process $cand.FullName -PassThru
$hwnd = Get-LanwWindowHandle -ProcessId $p.Id -TimeoutSeconds 30
Start-Sleep -Seconds 8
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

[void](Invoke-LanwByName -Root $root -Name $T_ACCOUNTS)
Start-Sleep -Seconds 5
[void](Invoke-LanwByName -Root $root -Name $T_RANDOM)
Write-Host "INFO  random login clicked; sampling dialog state"

$desktop = [System.Windows.Automation.AutomationElement]::RootElement
for ($t = 4; $t -le 20; $t += 4) {
    Start-Sleep -Seconds 4
    foreach ($scope in @(@('window', $root), @('desktop', $desktop))) {
        try {
            $els = $scope[1].FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            $texts = @($els | ForEach-Object { $_.Current.Name } | Where-Object { $_ -and $_.Length -gt 4 -and $_.Length -lt 140 })
            $hit = @($texts | Where-Object { $_ -match '[A-Za-z]{3,}|正在|无法|超时|失败|请点击|验证' })
            Write-Host ("t+{0,2}s [{1}] n={2}" -f $t, $scope[0], $texts.Count)
            $hit | Select-Object -Unique | Select-Object -First 8 | ForEach-Object { "        " + $_ }
        } catch { Write-Host ("t+{0,2}s [{1}] read failed: {2}" -f $t, $scope[0], $_.Exception.Message) }
    }
}

[void](Save-LanwScreenshot -WindowHandle $hwnd -Path 'D:\ku\traecode\lanw\probe_geetest2.png')
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
