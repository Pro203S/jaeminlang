@echo off
setlocal enabledelayedexpansion

set PROJECT=.\jaeminlang\jaeminlang.csproj
set CONFIG=Release

if not exist publish mkdir publish
if not exist artifacts mkdir artifacts

set RIDS=^
win-x64 ^
win-x86 ^
win-arm64 ^
osx-x64 ^
osx-arm64 ^
linux-x64 ^
linux-arm64 ^
linux-arm ^
linux-musl-x64 ^
linux-musl-arm64

for %%R in (%RIDS%) do (
    echo ========================================
    echo Publishing for %%R
    echo ========================================

    set OUTPUT_DIR=publish\%%R
    set ZIP_PATH=artifacts\%%R.zip

    dotnet publish "%PROJECT%" ^
        -c %CONFIG% ^
        -r %%R ^
        --self-contained true ^
        -p:PublishSingleFile=true ^
        -o "!OUTPUT_DIR!"

    if errorlevel 1 (
        echo Build failed for %%R
        exit /b 1
    )

    echo Compressing %%R...

    powershell -Command ^
        "if (Test-Path '!ZIP_PATH!') { Remove-Item '!ZIP_PATH!' }; Compress-Archive -Path '!OUTPUT_DIR!\*' -DestinationPath '!ZIP_PATH!'"

    if errorlevel 1 (
        echo Compression failed for %%R
        exit /b 1
    )

    echo %%R done!
    echo.
)

echo ========================================
echo All builds completed!
echo Artifacts are in .\artifacts
echo ========================================