@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD_AND_INSTALL.ps1"
if errorlevel 1 (
  echo.
  echo Build/install failed.
  pause
  exit /b 1
)
echo.
echo Build/install complete.
pause
