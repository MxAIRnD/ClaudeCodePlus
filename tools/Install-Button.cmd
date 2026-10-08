@echo off
rem Start this file with:  explorer.exe "<repo>\tools\Install-Button.cmd"
rem (an unpackaged context; see the .ps1 header). Output goes to tools\_install.log next to this file.
rem Add -Force after the .ps1 path to replace an existing shortcut.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Button.ps1" %* > "%~dp0_install.log" 2>&1
echo Exit code: %ERRORLEVEL% >> "%~dp0_install.log"
