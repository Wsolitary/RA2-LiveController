<#
.SYNOPSIS
    Builds RA2LiveController as a standalone, self-contained single-file win-x86 executable.
#>
[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Write-Host "=== RA2-LiveController Build Script ===" -ForegroundColor Cyan

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Error "Error: .NET SDK not found. Please install .NET 10 SDK from https://dotnet.microsoft.com/"
    exit 1
}

$projPath = Join-Path $PSScriptRoot "RA2LiveController.csproj"
Write-Host "[*] Publishing single-file ($Configuration, win-x86)..." -ForegroundColor Yellow

dotnet publish $projPath -c $Configuration -r win-x86 --self-contained true -p:PublishSingleFile=true

if ($LASTEXITCODE -eq 0) {
    $outExe = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\win-x86\publish\RA2LiveController.exe"
    Write-Host "`n[+] Build successful!" -ForegroundColor Green
    Write-Host "[+] Executable: $outExe" -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE."
}
