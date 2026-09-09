<#
.SYNOPSIS
    註冊吉伊卡哇桌面寵物（ChiikawaDesktopPet）為開機自動啟動工作排程任務。

.DESCRIPTION
    本腳本使用 Windows 工作排程器（Task Scheduler）註冊開機啟動任務，
    並針對長期背景運行的桌面寵物進行關鍵參數優化：
    - ExecutionTimeLimit = 0 (永不逾時終止，解決預設 3 天或數小時被 Task Scheduler 靜悄悄關閉的問題)
    - DontStopIfGoingOnBatteries = $true (筆記型電腦拔除電源使用電池時不中斷運行)
    - DisallowStartIfOnBatteries = $false (電池模式下開機仍正常啟動)
    - MultipleInstances = IgnoreNew (已有實體運行時不重複啟動)
    - RestartCount = 3, RestartInterval = 1分鐘 (若進程異常終止自動重啟最多3次)

.PARAMETER TaskName
    工作排程器任務名稱，預設為 "ChiikawaDesktopPet"

.PARAMETER ExePath
    可執行檔路徑。若未指定，腳本會自動搜尋本專案建置目錄下的 ChiikawaDesktopPet.exe
#>

[CmdletBinding()]
param (
    [string]$TaskName = "ChiikawaDesktopPet",
    [string]$ExePath = ""
)

# 若未指定 ExePath，自動搜尋常見輸出目錄
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $candidates = @(
        "$PSScriptRoot\..\src\ChiikawaDesktopPet.Wpf\bin\Release\net10.0-windows\win-x64\ChiikawaDesktopPet.exe",
        "$PSScriptRoot\..\src\ChiikawaDesktopPet.Wpf\bin\Debug\net10.0-windows\ChiikawaDesktopPet.exe",
        "$PSScriptRoot\ChiikawaDesktopPet.exe"
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $ExePath = (Resolve-Path $cand).Path
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ExePath) -or -not (Test-Path $ExePath)) {
    Write-Error "找不到 ChiikawaDesktopPet.exe。請使用 -ExePath 參數指定完整檔案路徑。"
    exit 1
}

$ExePath = (Resolve-Path $ExePath).Path
$WorkingDirectory = Split-Path -Path $ExePath

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "正在設定開機啟動工作排程任務: $TaskName" -ForegroundColor Cyan
Write-Host "執行檔案路徑: $ExePath" -ForegroundColor Gray
Write-Host "工作目錄:     $WorkingDirectory" -ForegroundColor Gray
Write-Host "==================================================" -ForegroundColor Cyan

# 1. 觸發程序: 使用者登入時啟動
$trigger = New-ScheduledTaskTrigger -AtLogOn

# 2. 動作: 啟動主程式並指派工作目錄 (確保相對路徑 assets/config.json 正常讀取)
$action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $WorkingDirectory

# 3. 關鍵防關閉設定:
# - ExecutionTimeLimit: TimeSpan.Zero 代表永不因執行時間過長而強制殺死進程 (解決靜默閃退核心元凶)
# - AllowStartIfOnBatteries / DontStopIfGoingOnBatteries: 電池模式不受影響
# - MultipleInstances: IgnoreNew
# - RestartCount: 3 次重試
$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -MultipleInstances IgnoreNew `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -StartWhenAvailable

# 4. 主體使用者身分 (目前登入的使用者，互動模式)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

try {
    Register-ScheduledTask `
        -TaskName $TaskName `
        -Trigger $trigger `
        -Action $action `
        -Settings $settings `
        -Principal $principal `
        -Description "吉伊卡哇桌面寵物開機自動啟動工作 (無時間限制、防休眠與電池中斷)" `
        -Force | Out-Null

    Write-Host "✔ 成功註冊工作排程任務: $TaskName！" -ForegroundColor Green
    Write-Host "重要特性：" -ForegroundColor Yellow
    Write-Host "  1. 永不逾時終止 (ExecutionTimeLimit = 0)" -ForegroundColor White
    Write-Host "  2. 筆電切換電池不中斷 (DontStopIfGoingOnBatteries = True)" -ForegroundColor White
    Write-Host "  3. 異常終止自動重試 (RestartCount = 3)" -ForegroundColor White
}
catch {
    Write-Error "註冊排程工作失敗: $_"
    exit 1
}
