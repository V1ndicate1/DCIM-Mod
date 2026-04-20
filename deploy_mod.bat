@echo off
setlocal

set GAME_MODS=D:\SteamLibrary\steamapps\common\Data Center\Data Center_Data\StreamingAssets\Mods
set SOURCE_DIR=%~dp0

if "%~1"=="" (
    echo Usage: deploy_mod.bat ModFolderName [remove]
    echo.
    echo Examples:
    echo   deploy_mod.bat MyCustomServer
    echo   deploy_mod.bat MyCustomServer remove
    echo.
    echo Available mods in this folder:
    for /d %%D in ("%SOURCE_DIR%*") do (
        if not "%%~nxD"=="_template" echo   %%~nxD
    )
    exit /b 1
)

set MOD_NAME=%~1
set SOURCE=%SOURCE_DIR%%MOD_NAME%
set DEST=%GAME_MODS%\%MOD_NAME%

if not exist "%SOURCE%" (
    echo ERROR: Mod folder not found: %SOURCE%
    exit /b 1
)

if /i "%~2"=="remove" (
    if not exist "%DEST%" (
        echo Mod "%MOD_NAME%" is not deployed.
    ) else (
        rmdir /s /q "%DEST%"
        echo Removed: %MOD_NAME%
    )
    exit /b 0
)

echo Deploying: %MOD_NAME%
echo   From: %SOURCE%
echo   To:   %DEST%

if exist "%DEST%" (
    echo Removing old version...
    rmdir /s /q "%DEST%"
)

xcopy /e /i /q "%SOURCE%" "%DEST%"

if %errorlevel%==0 (
    echo.
    echo SUCCESS: %MOD_NAME% deployed.
    echo Launch the game and check the shop.
) else (
    echo.
    echo ERROR: Deploy failed. Check paths above.
)
