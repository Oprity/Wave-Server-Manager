# Wave Server Manager — https://github.com/ (see README)
# Copyright (C) 2026 Wave Server Manager contributors
# SPDX-License-Identifier: AGPL-3.0-or-later

<#
.SYNOPSIS
  One-click launcher for Wave Server Manager (double-click start.bat).
.DESCRIPTION
  Checks prerequisites (.NET 10 SDK, Node 20+), rebuilds the web UI when sources
  are newer than the staged output, publishes the desktop app when needed, then
  launches it. Pass -Smoke to run the headless self-test instead of the window.
#>
param(
  [switch]$Smoke
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$webDir = Join-Path $root 'src/Web'
$wwwroot = Join-Path $root 'src/Server/wwwroot/index.html'
$exe = Join-Path $root 'desktop-publish/WaveServerManager.Desktop.exe'

function Test-Command($name) {
  return $null -ne (Get-Command $name -ErrorAction SilentlyContinue)
}

function Get-Newest([string]$path, [string]$filter) {
  $f = Get-ChildItem -Path $path -Recurse -File -Filter $filter -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  return $f
}

# --- prerequisites -----------------------------------------------------------
if (-not (Test-Command 'dotnet')) {
  Write-Error 'Missing .NET SDK. Install .NET 10 from https://dotnet.microsoft.com/download and re-run.'
}
$sdks = (& dotnet --list-sdks) -join "`n"
if ($sdks -notmatch '10\.') {
  Write-Error "Missing .NET 10 SDK (found: $sdks). Install it from https://dotnet.microsoft.com/download and re-run."
}
if (-not (Test-Command 'node') -or -not (Test-Command 'npm')) {
  Write-Error 'Missing Node.js. Install Node 20+ from https://nodejs.org/ and re-run.'
}

# --- frontend ----------------------------------------------------------------
$srcNewest = Get-Newest (Join-Path $webDir 'src') '*.tsx'
$wwwrootMissing = -not (Test-Path $wwwroot)
$stale = $wwwrootMissing -or ($null -ne $srcNewest -and $srcNewest.LastWriteTime -gt (Get-Item $wwwroot).LastWriteTime)
if ($stale) {
  Write-Host 'Building web UI...'
  Push-Location $webDir
  try {
    if (-not (Test-Path (Join-Path $webDir 'node_modules'))) {
      npm install
      if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }
    }
    npm run build:server
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
  }
  finally {
    Pop-Location
  }
}
else {
  Write-Host 'Web UI is up to date.'
}

# --- desktop publish ----------------------------------------------------------
$csNewest = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
$exeMissing = -not (Test-Path $exe)
$exeStale = $exeMissing -or ($null -ne $csNewest -and $csNewest.LastWriteTime -gt (Get-Item $exe).LastWriteTime)
if ($exeStale) {
  Write-Host 'Publishing desktop app (first run takes a while)...'
  & dotnet publish (Join-Path $root 'src/Desktop') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $root 'desktop-publish')
  if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
}
else {
  Write-Host 'Desktop app is up to date.'
}

# --- launch -------------------------------------------------------------------
# NOTE: the desktop app is a WinExe (no console), so `&` would not wait for it.
# Start-Process -Wait is required to observe the smoke-test exit code.
if ($Smoke) {
  Write-Host 'Running headless self-test...'
  $proc = Start-Process -FilePath $exe -ArgumentList '--smoke' -Wait -PassThru
  exit $proc.ExitCode
}

Write-Host 'Starting Wave Server Manager...'
Start-Process -FilePath $exe
