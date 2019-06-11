@echo off
SETLOCAL

REM https://stackoverflow.com/a/45070967

goto :init

:usage
    echo MixedReality-Sharing MSVC solution generator
    echo.
    echo USAGE:
    echo   create_sln.cmd [flags]
    echo.
    echo.  -h, --help               shows this help
    echo.  -v, --verbose            shows detailed output
    goto :eof

:init
    set "OPT_HELP="

    pushd %~dp0..\
    set "REPO_ROOT_PATH=%CD%"
    popd

:parse
    if "%~1"=="" goto :main

    if /i "%~1"=="/h"         call :usage "%~2" & goto :end
    if /i "%~1"=="-h"         call :usage "%~2" & goto :end
    if /i "%~1"=="--help"     call :usage "%~2" & goto :end

    shift
    goto :parse

:main

    set SLN_ROOT=%REPO_ROOT_PATH%\build\VS17-x64
    cmake "%REPO_ROOT_PATH%" "-B%SLN_ROOT%" -G"Visual Studio 15 2017 Win64"

:end
    exit /B
