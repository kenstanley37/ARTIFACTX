# Builds an ArtifactX release: a self-contained x64 publish, packed by
# Velopack into Setup.exe, a portable zip and the update package.
#
#   pwsh scripts/release.ps1
#
# The version comes from <Version> in src/ArtifactX.WinUI3/ArtifactX.WinUI3.csproj
# (e.g. 1.0.0-alpha1), so the app, the packages and the GitHub release tag
# (v + version) always agree. Needs the vpk tool: dotnet tool install -g vpk
# Output: artifacts/release/<version>/ (gitignored).

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src/ArtifactX.WinUI3/ArtifactX.WinUI3.csproj'

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project" }
Write-Host "ArtifactX $version" -ForegroundColor Cyan

$out = Join-Path $repo "artifacts/release/$version"
$publish = Join-Path $out 'publish'
$packages = Join-Path $out 'packages'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish $project -c Release -p:Platform=x64 -p:PublishProfile=win-x64 -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# --skipVeloAppCheck: VelopackApp.Build().Run() is in App()'s constructor (WinUI 3
# has no Main of our own), which vpk's static check can't see.
# Velopack installs to %LocalAppData%\ArtifactX and manages that whole
# folder (an uninstall removes it), so the app keeps its own data in
# %AppData%\ArtifactX instead (LocalAppDataPaths, moved there 2026-10-15).
vpk pack --packId ArtifactX --packVersion $version `
    --packDir $publish --mainExe ArtifactX.exe `
    --icon (Join-Path $repo 'src/ArtifactX.WinUI3/Assets/AppIcon.ico') `
    --packTitle ArtifactX --skipVeloAppCheck `
    --outputDir $packages
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

Write-Host "`nDone. Upload these to a GitHub release tagged v${version}:" -ForegroundColor Green
Get-ChildItem $packages | ForEach-Object { Write-Host "  $($_.Name)  ($([math]::Round($_.Length / 1MB, 1)) MB)" }
