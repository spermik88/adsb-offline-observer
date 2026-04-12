@echo off
setlocal
set ZADIG=%~dp0zadig.exe

if not exist "%ZADIG%" (
  echo Bundled Zadig helper was not found.
  exit /b 1
)

start "" "%ZADIG%"
exit /b 0
