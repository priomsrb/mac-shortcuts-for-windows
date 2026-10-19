# Builds the release exe locally, the same way .github/workflows/release.yml does:
# publishes with Native AOT and writes MacShortcuts-vX.Y.Z-win-x64.exe plus a .sha256 file.
#   tools/build-release.ps1              output goes to publish/release
#   tools/build-release.ps1 -SkipTests   skip `dotnet test`
#   tools/build-release.ps1 -OutDir out  put the files somewhere else
param(
    [string]$OutDir = 'publish/release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$csproj = 'src/MacShortcuts/MacShortcuts.csproj'
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> found in $csproj." }

# The Native AOT linker lookup needs vswhere.exe on PATH (see README).
$installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if ((Test-Path $installer) -and -not (Get-Command vswhere -ErrorAction SilentlyContinue)) {
    $env:PATH = "$installer;$env:PATH"
}

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed" }
}

if (-not $SkipTests) { Invoke-Dotnet test -c Release }

$staging = Join-Path $root 'publish/staging'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
Invoke-Dotnet publish src/MacShortcuts -c Release -r win-x64 -o $staging

New-Item -ItemType Directory -Force $OutDir | Out-Null
$name = "MacShortcuts-v$version-win-x64.exe"
$exe = Join-Path $OutDir $name
Move-Item (Join-Path $staging 'MacShortcuts.exe') $exe -Force
Remove-Item $staging -Recurse -Force

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLower()
"$hash  $name" | Out-File "$exe.sha256" -Encoding ascii

$size = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "Built $exe ($size MB)"
Write-Host "SHA256 $hash"
