@echo off
set LOG=C:\Users\Jacob\AppData\LocalLow\WASEKU\Data Center\Player.log

echo ============================================
echo  Data Center - Mod Debug Log Watcher
echo ============================================
echo  Watching: %LOG%
echo  Filtering: [ModLoader] messages
echo  Press Ctrl+C to stop
echo ============================================
echo.

if not exist "%LOG%" (
    echo Log file not found. Launch the game first.
    pause
    exit /b 1
)

powershell -Command "
    \$log = '%LOG%'
    \$lastSize = 0
    Write-Host 'Watching for [ModLoader] entries... (launch the game now)' -ForegroundColor Cyan
    Write-Host ''
    while (\$true) {
        if (Test-Path \$log) {
            \$currentSize = (Get-Item \$log).Length
            if (\$currentSize -ne \$lastSize) {
                \$content = Get-Content \$log -Raw
                \$lines = \$content -split '\`n'
                \$modLines = \$lines | Where-Object { \$_ -match '\[ModLoader\]|\[MyMod\]|mod.*error|Error.*mod' }
                if (\$modLines) {
                    foreach (\$line in \$modLines) {
                        \$trimmed = \$line.Trim()
                        if (\$trimmed -ne '') {
                            if (\$trimmed -match 'error|fail|not found|invalid' ) {
                                Write-Host \$trimmed -ForegroundColor Red
                            } elseif (\$trimmed -match 'loaded|success') {
                                Write-Host \$trimmed -ForegroundColor Green
                            } else {
                                Write-Host \$trimmed -ForegroundColor Yellow
                            }
                        }
                    }
                }
                \$lastSize = \$currentSize
            }
        }
        Start-Sleep -Milliseconds 500
    }
"
