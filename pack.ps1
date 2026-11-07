# Builds Uter the Porter and makes the Thunderstore upload zip in .\dist
# (manifest.json, README.md, CHANGELOG.md, icon.png and UterPorter.dll, all at the top of the zip).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build UterPorter.csproj -c Release | Out-Host

$manifest = Get-Content Package\manifest.json -Raw | ConvertFrom-Json
$version = $manifest.version_number
$staging = Join-Path $PSScriptRoot "dist\staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force $staging | Out-Null

Copy-Item Package\manifest.json, Package\README.md, Package\CHANGELOG.md, Package\icon.png $staging
Copy-Item bin\Release\UterPorter.dll $staging

$zip = Join-Path $PSScriptRoot "dist\Uter_the_Porter-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
Remove-Item $staging -Recurse -Force
Write-Host "Made $zip"
