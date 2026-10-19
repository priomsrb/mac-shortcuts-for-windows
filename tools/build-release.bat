@echo off
rem Double-click to build the release exe (runs tools\build-release.ps1; extra args are passed on, e.g. -SkipTests).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" %*
set EXITCODE=%ERRORLEVEL%
echo.
if %EXITCODE%==0 (echo Done.) else (echo Build failed with exit code %EXITCODE%.)
pause
exit /b %EXITCODE%
