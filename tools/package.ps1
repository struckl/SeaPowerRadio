<#
.SYNOPSIS
    Builds a release of Sea Power Radio and stages the folder that goes to the Steam Workshop.

.DESCRIPTION
    Release build without the automatic deploy, then dist\SeaPowerRadio\ with exactly what players get:
    SeaPowerRadio.dll, _info.ini, LICENSE.txt, CREDITS.txt (the voices' CC BY 4.0 attribution), the Mod Manager
    thumbnail (workshop\thumbnail.png, if present) and voices\ – the catalog files and the rendered library from
    mod\voices\ (not in git; rendered with tools\voicegen). Refuses to stage a library with personas missing.
    -Install mirrors the staged folder into <GameDir>\Sea Power_Data\StreamingAssets\SeaPowerRadio\, the folder to
    pick in the game's Create Mod screen (Sea Power must be closed). -Zip also writes dist\SeaPowerRadio-<version>.zip
    for a GitHub release.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
#>
param(
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { 'E:\Games\steamapps\common\Sea Power' }),
    [switch]$Install,
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\SeaPowerRadio\SeaPowerRadio.csproj'
$dist = Join-Path $root 'dist\SeaPowerRadio'
$voices = Join-Path $root 'mod\voices'

# Every persona in personas.txt needs its rendered folder.
$personas = Get-Content (Join-Path $voices 'personas.txt') | Where-Object { $_ -and -not $_.StartsWith('#') } |
    ForEach-Object { ($_ -split '[\s|,;=]+')[0] }
$missing = $personas | Where-Object { -not (Test-Path (Join-Path $voices $_)) }
if ($missing) { throw "Rendered voices missing for: $($missing -join ', ') - render them (tools\voicegen) first." }

dotnet build $project -c Release -p:Deploy=false -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$out = Join-Path $root 'src\SeaPowerRadio\bin\Release\netstandard2.1'
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $out 'SeaPowerRadio.dll') $dist
Copy-Item (Join-Path $root 'mod\_info.ini') $dist
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $dist 'LICENSE.txt')
Copy-Item (Join-Path $root 'VOICES-LICENSE.md') (Join-Path $dist 'CREDITS.txt')
$thumbnail = Join-Path $root 'workshop\thumbnail.png'
if (Test-Path $thumbnail) { Copy-Item $thumbnail $dist } else { Write-Warning 'workshop\thumbnail.png missing - no Mod Manager picture.' }

# robocopy is far faster than Copy-Item for ~57,000 small clips; half-written takes (*.part.mp3) stay behind.
robocopy $voices (Join-Path $dist 'voices') /E /XF *.part.mp3 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the voice library failed (robocopy $LASTEXITCODE)." }

$version = (Select-String -Path (Join-Path $root 'src\SeaPowerRadio\Plugin.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
$clips = (Get-ChildItem -Recurse -File -Filter *.mp3 (Join-Path $dist 'voices')).Count
Write-Host "Staged Sea Power Radio $version in $dist ($($personas.Count) voices, $clips clips)"

if ($Zip) {
    $archive = Join-Path $root "dist\SeaPowerRadio-$version.zip"
    if (Test-Path $archive) { Remove-Item -Force $archive }
    Compress-Archive -Path $dist -DestinationPath $archive -CompressionLevel Fastest
    Write-Host "Release zip: $archive"
}

if ($Install) {
    if (Get-Process -Name 'Sea Power' -ErrorAction SilentlyContinue) { throw 'Sea Power is running - close it first.' }
    $target = Join-Path $GameDir 'Sea Power_Data\StreamingAssets\SeaPowerRadio'
    robocopy $dist $target /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Installing failed (robocopy $LASTEXITCODE)." }
    Write-Host "Installed to $target - pick this folder in Mods > Create Mod."
}
