@echo off
REM Wave Server Manager one-click launcher (AGPL-3.0-or-later, see LICENSE).
REM Double-click this file. Pass --smoke for the headless self-test.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-WaveServerManager.ps1" %*
