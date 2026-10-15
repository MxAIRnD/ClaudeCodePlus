@echo off
rem Start this file with:  explorer.exe "<repo>\tools\Capture-Documents.cmd"
rem A read-only capture of the Claude window's page documents and the newest session record (see
rem the .ps1 header), for research/CAPTURES_2026-10-15_v1.md. Then switch to the Claude window and
rem do the thing to capture; the capture runs 25 s. Output is appended to tools\_capture.log next to
rem this file. Extra arguments go to the .ps1, e.g.:  Capture-Documents.cmd -Seconds 40
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Capture-Documents.ps1" %* >> "%~dp0_capture.log" 2>&1
echo Exit code: %ERRORLEVEL% >> "%~dp0_capture.log"
