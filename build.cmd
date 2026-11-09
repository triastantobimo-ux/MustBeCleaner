@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "CONFIG=Debug"
set "DOTEST=1"

:args
if "%~1"=="" goto resolve
if /I "%~1"=="-Configuration" goto set_config
if /I "%~1"=="--configuration" goto set_config
if /I "%~1"=="-c" goto set_config
if /I "%~1"=="-NoTest" goto no_test
if /I "%~1"=="--no-test" goto no_test
echo Unknown argument: %~1
echo Usage: build.cmd [-Configuration Debug^|Release] [-NoTest]
exit /b 2

:set_config
if "%~2"=="" (
    echo Missing value for %~1
    exit /b 2
)
set "CONFIG=%~2"
shift
shift
goto args

:no_test
set "DOTEST=0"
shift
goto args

:resolve
set "DOTNET="
if defined DOTNET_ROOT call :try "%DOTNET_ROOT%\dotnet.exe"
call :try "%USERPROFILE%\.dotnet10\dotnet.exe"
call :try "%ProgramFiles%\dotnet\dotnet.exe"
call :try "%ProgramW6432%\dotnet\dotnet.exe"
for /f "delims=" %%D in ('where dotnet 2^>nul') do call :try "%%D"

if not defined DOTNET (
    echo SDK 10 not found. Install it or set DOTNET_ROOT; global.json requires 10.0.100 ^(rollForward latestFeature^).
    exit /b 1
)

echo Using %DOTNET%
"%DOTNET%" build "%ROOT%MustBeCleaner.sln" -c %CONFIG% -m:1
if errorlevel 1 exit /b 1

if "%DOTEST%"=="1" (
    "%DOTNET%" test "%ROOT%MustBeCleaner.sln" -c %CONFIG% -m:1 --no-build
    if errorlevel 1 exit /b 1
)
exit /b 0

:try
if defined DOTNET exit /b 0
if not exist "%~1" exit /b 0
"%~1" --list-sdks 2>nul | findstr /r /c:"^10\." >nul
if errorlevel 1 exit /b 0
set "DOTNET=%~1"
exit /b 0
