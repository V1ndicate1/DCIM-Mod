@echo off
setlocal

if "%~1"=="" (
    echo Usage: validate_config.bat ModFolderName
    echo.
    echo Checks config.json for common errors before deploying.
    exit /b 1
)

set MOD_NAME=%~1
set CONFIG=%~dp0%MOD_NAME%\config.json

if not exist "%CONFIG%" (
    echo ERROR: No config.json found in "%MOD_NAME%"
    exit /b 1
)

echo Validating: %CONFIG%
echo.

powershell -Command "
    \$config = '%CONFIG%'
    \$errors = 0
    \$warnings = 0

    # Check JSON is valid
    try {
        \$json = Get-Content \$config -Raw | ConvertFrom-Json
        Write-Host '[OK] JSON syntax is valid' -ForegroundColor Green
    } catch {
        Write-Host '[ERROR] Invalid JSON: ' + \$_.Exception.Message -ForegroundColor Red
        exit 1
    }

    # Check required top-level fields
    if (-not \$json.modName) { Write-Host '[ERROR] Missing: modName' -ForegroundColor Red; \$errors++ }
    else { Write-Host '[OK] modName: ' + \$json.modName -ForegroundColor Green }

    # Check shopItems
    if (\$json.shopItems -and \$json.shopItems.Count -gt 0) {
        Write-Host '[OK] shopItems: ' + \$json.shopItems.Count + ' item(s)' -ForegroundColor Green
        \$i = 0
        foreach (\$item in \$json.shopItems) {
            \$i++
            Write-Host '  Item ' + \$i + ':' -ForegroundColor Cyan

            if (-not \$item.PSObject.Properties['price']) { Write-Host '    [ERROR] Missing: price' -ForegroundColor Red; \$errors++ }
            else { Write-Host '    [OK] price: ' + \$item.price -ForegroundColor Green }

            if (-not \$item.PSObject.Properties['sizeInU']) { Write-Host '    [WARN] Missing: sizeInU (defaults may apply)' -ForegroundColor Yellow; \$warnings++ }
            else { Write-Host '    [OK] sizeInU: ' + \$item.sizeInU -ForegroundColor Green }

            # Check files exist
            \$modFolder = Split-Path \$config
            foreach (\$fileField in @('modelFile', 'textureFile', 'iconFile')) {
                \$fileName = \$item.\$fileField
                if (\$fileName) {
                    \$filePath = Join-Path \$modFolder \$fileName
                    if (Test-Path \$filePath) {
                        Write-Host ('    [OK] ' + \$fileField + ': ' + \$fileName + ' (found)') -ForegroundColor Green
                    } else {
                        Write-Host ('    [ERROR] ' + \$fileField + ': ' + \$fileName + ' NOT FOUND') -ForegroundColor Red
                        \$errors++
                    }
                } else {
                    Write-Host ('    [WARN] ' + \$fileField + ': not specified') -ForegroundColor Yellow
                    \$warnings++
                }
            }
        }
    } else {
        Write-Host '[INFO] No shopItems defined' -ForegroundColor Gray
    }

    Write-Host ''
    if (\$errors -eq 0 -and \$warnings -eq 0) {
        Write-Host 'All checks passed. Ready to deploy.' -ForegroundColor Green
    } elseif (\$errors -eq 0) {
        Write-Host (\$warnings.ToString() + ' warning(s). Mod may work but review warnings above.') -ForegroundColor Yellow
    } else {
        Write-Host (\$errors.ToString() + ' error(s) found. Fix before deploying.') -ForegroundColor Red
    }
"
