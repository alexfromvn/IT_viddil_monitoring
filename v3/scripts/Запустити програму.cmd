@echo off
setlocal
set "IT_VIDDIL_APP_DIR=%~dp0"
set "IT_VIDDIL_LAUNCH_MODE=%~1"
set "IT_VIDDIL_POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%IT_VIDDIL_POWERSHELL%" (
  echo Windows PowerShell is unavailable on this computer.
  pause
  exit /b 1
)
"%IT_VIDDIL_POWERSHELL%" -NoLogo -NoProfile -NonInteractive -Command "$ErrorActionPreference = 'Stop'; try { Import-Module -Name (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop; $mode = [string]$env:IT_VIDDIL_LAUNCH_MODE; if (@('', '--check-only', '--unblock-only') -notcontains $mode) { throw 'Unknown option. Run the launcher without arguments.' }; $folder = [IO.Path]::GetFullPath($env:IT_VIDDIL_APP_DIR); $app = Join-Path $folder 'IT_viddil_monitoring_v3.exe'; if (-not (Test-Path -LiteralPath $app -PathType Leaf)) { throw 'IT_viddil_monitoring_v3.exe is missing. Extract the entire ZIP first and keep the launcher beside the EXE.' }; $item = Get-Item -LiteralPath $app -Force; if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'The EXE must be a regular file, not a link.' }; $known = @('d2ca0ec05178be20b3846f68e641c7cb832448cc425e6c980a6e682b66c8c496','bee79c6562df78e0547bdf3dd9d9a2bd3adfcf05a79fc56e7b6ccdc578ac4800','6d409944da6df6f71adb7e0f4b52a92d41d49f3c78984f3e7240391b65c0386c','68af65db66cdd52cc8d41458a7791be50dbe48668ed572b7e75c32a13ff4dc98','e55b7060924d1cf7734c2330d4b166ceb7142b9429b831563cc42b9ef273fd59','9b93f82daa4216af51ae2c7dc36ba1e1bed557c5d41ad90f9451e7617054397e','084a7bf97e1b629b402d6d9eced3c466b65883f7ff77bdab8b6b2d97465b6b2c','edcd5f24c5eda408159cb40d9820456ad6efa3d29dd56c662ac3cfd0ce0f3ae5'); $hash = (Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash.ToLowerInvariant(); if ($known -notcontains $hash) { throw 'This EXE is not a recognized release, or its contents have changed. It was NOT unblocked or launched. Obtain the original package from the maintainer.' }; Write-Host 'IT_viddil_monitoring: original release verified.'; if ($mode -eq '--check-only') { exit 0 }; Unblock-File -LiteralPath $app -ErrorAction Stop; $zone = Get-Item -LiteralPath $app -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue; if ($null -ne $zone) { throw 'Windows did not remove the download marker. Check folder permissions or contact the computer administrator.' }; $after = (Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash.ToLowerInvariant(); if ($after -ne $hash) { throw 'The EXE changed during verification. Launch cancelled.' }; Write-Host 'The download marker has been removed for this EXE.'; if ($mode -eq '--unblock-only') { exit 0 }; Start-Process -FilePath $app -WorkingDirectory $folder -ErrorAction Stop | Out-Null; exit 0 } catch { Write-Host ('ERROR: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
set "IT_VIDDIL_EXIT=%ERRORLEVEL%"
if not "%IT_VIDDIL_EXIT%"=="0" if "%IT_VIDDIL_LAUNCH_MODE%"=="" pause
exit /b %IT_VIDDIL_EXIT%

