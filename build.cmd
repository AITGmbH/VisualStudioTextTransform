@echo off
rem Printf environment info
set

if exist bootstrap.cmd (
  call bootstrap.cmd
  if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%
)

set buildFile=build.fsx
"packages/AIT.Build/content/build.cmd" %*
