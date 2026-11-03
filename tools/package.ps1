<#
.SYNOPSIS
    Builds a release of Sea Power Radio and stages the folder that goes to the Steam Workshop.

.DESCRIPTION
    Release builds without the automatic deploy, then dist\SeaPowerRadio\ with exactly what players get:
    SeaPowerRadio.dll (the 3D-globe game) and SeaPowerRadioFlat.dll (Sea Power 0.8.7's flat map), the same sources
    built against each game's assemblies, each staying off on the other game; _info.ini, rats-radio-aliases.txt (default hull prefixes), LICENSE.txt, CREDITS.txt (the voices' CC BY 4.0 attribution), voices\ – the
    catalog files and the rendered library from mod\voices\ (not in git; rendered with tools\voicegen) – and
    preview.jpg, the Workshop preview (workshop\preview.jpg, if present): the game's upload screen only browses
    StreamingAssets for it. Refuses to stage a library with personas missing.
    The flat build needs a copy of 0.8.7's Sea Power_Data\Managed (-FlatManagedDir; default: _gamerefs next to the mod
    folders in the workspace), the globe build the globe game's (-GlobeManagedDir when the installed game is 0.8.7).
    When the workspace's tools\compat-check is there, every DLL is checked against both games' assemblies: it must
    have nothing missing on its own game, and its entry point nothing missing on either. -NoFlat leaves the flat build out.
    -Install mirrors the staged folder into <GameDir>\Sea Power_Data\StreamingAssets\SeaPowerRadio\, the folder to
    pick in the game's Create Mod screen (Sea Power must be closed). -Zip also writes dist\SeaPowerRadio-<version>.zip
    for a GitHub release.
    -Pack <id> stages a voice pack instead of the base: the folder with that voicepack.ini Id (looked up in mod\voices and
    in packs\*, e.g. a pack under work) goes to dist\pack-<id>\ as it is (voicepack.ini, personas.txt, catalog.txt, names.txt, clips;
    plus the pack's own _info.ini and CREDITS if it has them) – the folder to upload to that pack's Workshop item. No
    build, no DLL: packs never carry code. -Zip also writes dist\SeaPowerRadio-pack-<id>-<version>.zip.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
#>
param(
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { 'E:\Games\steamapps\common\Sea Power' }),
    [string]$FlatManagedDir = $(if ($env:FlatManagedDir) { $env:FlatManagedDir } else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\_gamerefs\SeaPower-0.8.7-public\Managed')) }),
    # The globe build's game assemblies when the installed game isn't the globe version (a saved copy of its Managed).
    [string]$GlobeManagedDir = $env:GlobeManagedDir,
    [switch]$NoFlat,
    [switch]$Install,
    [switch]$Zip,
    [string]$Pack
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\SeaPowerRadio\SeaPowerRadio.csproj'
$dist = Join-Path $root 'dist\SeaPowerRadio'
$voices = Join-Path $root 'mod\voices'

# -Pack <id>: stage one voice pack and stop (no build: a pack has no DLL).
if ($Pack) {
    $source = @((Join-Path $root 'mod\voices')) + @(Get-ChildItem -Directory (Join-Path $root 'packs') -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }) |
        Where-Object { (Test-Path (Join-Path $_ 'voicepack.ini')) -and ((Get-Content (Join-Path $_ 'voicepack.ini')) -match "^\s*Id\s*=\s*$([regex]::Escape($Pack))\s*(;.*)?$") } |
        Select-Object -First 1
    if (-not $source) { throw "No voice pack with Id '$Pack' (looked for voicepack.ini in mod\voices and packs\*)." }
    $ini = Get-Content (Join-Path $source 'voicepack.ini')
    $packVersion = ($ini | Where-Object { $_ -match '^\s*Version\s*=' } | Select-Object -First 1) -replace '^\s*Version\s*=\s*([^\s;]+).*$', '$1'
    $ids = Get-Content (Join-Path $source 'personas.txt') | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { ($_ -split '[\s|,;=]+')[0] }
    $lacking = $ids | Where-Object { -not (Test-Path (Join-Path $source $_)) }
    if ($lacking) { throw "Rendered voices missing for: $($lacking -join ', ')." }
    $target = Join-Path $root "dist\pack-$Pack"
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    robocopy $source $target /E /XF *.part.mp3 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying the pack failed (robocopy $LASTEXITCODE)." }
    $n = (Get-ChildItem -Recurse -File -Filter *.mp3 $target).Count
    Write-Host "Staged voice pack '$Pack' $packVersion in $target ($($ids.Count) voices, $n clips)"
    if ($Zip) {
        $archive = Join-Path $root "dist\SeaPowerRadio-pack-$Pack-$packVersion.zip"
        if (Test-Path $archive) { Remove-Item -Force $archive }
        Compress-Archive -Path $target -DestinationPath $archive -CompressionLevel Fastest
        Write-Host "Pack zip: $archive"
    }
    return
}

# Every persona in personas.txt needs its rendered folder.
$personas = Get-Content (Join-Path $voices 'personas.txt') | Where-Object { $_ -and -not $_.StartsWith('#') } |
    ForEach-Object { ($_ -split '[\s|,;=]+')[0] }
$missing = $personas | Where-Object { -not (Test-Path (Join-Path $voices $_)) }
if ($missing) { throw "Rendered voices missing for: $($missing -join ', ') - render them (tools\voicegen) first." }
if (-not (Test-Path (Join-Path $voices 'voicepack.ini'))) { throw 'mod\voices\voicepack.ini is missing: the base''s voices are the NATO pack.' }

$globeManaged = if ($GlobeManagedDir) { $GlobeManagedDir } else { Join-Path $GameDir 'Sea Power_Data\Managed' }

# The two builds differ only in the assemblies they compile against and one define: --no-incremental, or the second
# build may reuse the first one's output.
$out = Join-Path $root 'src\SeaPowerRadio\bin\Release\netstandard2.1'
dotnet build $project -c Release --no-incremental -p:Deploy=false -p:GameDir="$GameDir" -p:ManagedDir="$globeManaged"
if ($LASTEXITCODE -ne 0) { throw 'Globe build failed (with the game on 0.8.7, pass -GlobeManagedDir with the globe version''s assemblies).' }
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $out 'SeaPowerRadio.dll') $dist
if (-not $NoFlat) {
    if (-not (Test-Path (Join-Path $FlatManagedDir 'Seapower-Scripts.dll'))) { throw "No 0.8.7 assemblies in $FlatManagedDir (a copy of its Sea Power_Data\Managed, or pass -NoFlat)." }
    dotnet build $project -c Release --no-incremental -p:Deploy=false -p:GameDir="$GameDir" -p:GameBuild=Flat -p:FlatManagedDir="$FlatManagedDir"
    if ($LASTEXITCODE -ne 0) { throw 'Flat-map build failed.' }
    Copy-Item (Join-Path $out 'SeaPowerRadioFlat.dll') $dist
}

# Each DLL against both games' assemblies (the workspace's compat-check): nothing missing on its own game, and its
# entry point (which runs on both) nothing missing on either.
$tool = Join-Path $root '..\tools\compat-check'
if (Test-Path $tool) {
    dotnet build $tool -c Release -v q | Out-Null
    $gates = @('--gate', 'SeaPowerRadio.Plugin::TriggerEntryPoint', '--gate', 'SeaPowerRadio.Plugin::.ctor')
    foreach ($b in @(@('SeaPowerRadio.dll', $globeManaged, $FlatManagedDir), @('SeaPowerRadioFlat.dll', $FlatManagedDir, $globeManaged))) {
        $dll = Join-Path $dist $b[0]
        if (-not (Test-Path $dll)) { continue }
        dotnet run --project $tool -c Release --no-build -- $dll --against $b[1] --quiet
        if ($LASTEXITCODE -ne 0) { throw "$($b[0]) references things its own game does not have." }
        # --classload: Unity's ECS start-up loads EVERY type of every loaded assembly, so a field of a type the other game lacks
        # (not just the entry point) stops all missions from loading there, although the DLL stays off (found 2026-10-28).
        dotnet run --project $tool -c Release --no-build -- $dll --against $b[2] --quiet --classload @gates
        if ($LASTEXITCODE -ne 0) { throw "$($b[0]): the entry point or a type's fields would fail on the other game." }
    }
}
else { Write-Warning 'tools\compat-check not found (the workspace next to this repo) - the DLLs were not checked against both games.' }
Copy-Item (Join-Path $root 'mod\_info.ini') $dist
Copy-Item (Join-Path $root 'mod\rats-radio-aliases.txt') $dist
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $dist 'LICENSE.txt')
Copy-Item (Join-Path $root 'VOICES-LICENSE.md') (Join-Path $dist 'CREDITS.txt')
$preview = Join-Path $root 'workshop\preview.jpg'
if (Test-Path $preview) { Copy-Item $preview $dist } else { Write-Warning 'workshop\preview.jpg missing - nothing to pick as the Workshop preview.' }

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
