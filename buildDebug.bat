@echo off
setlocal

if not exist debug mkdir debug

set PROJECT=.\jaeminlang\jaeminlang.csproj
set CONFIG=Debug
set FRAMEWORK=net10.0
set BUILD_DIR=.\jaeminlang\bin\%CONFIG%\%FRAMEWORK%

echo ========================================
echo Building jaeminlang (%CONFIG%)
echo ========================================

dotnet build "%PROJECT%" -c %CONFIG%
if errorlevel 1 (
    echo Build failed
    exit /b 1
)

echo Copying build outputs to project root...

for %%F in (
    jaeminlang.exe
    jaeminlang.dll
    jaeminlang.deps.json
    jaeminlang.runtimeconfig.json
    jaeminlang.pdb
) do (
    if exist "%BUILD_DIR%\%%F" (
        copy /Y "%BUILD_DIR%\%%F" ".\debug\%%F" >nul
    )
)

echo ========================================
echo Debug build completed!
echo Root output: .\jaeminlang.exe
echo ========================================
