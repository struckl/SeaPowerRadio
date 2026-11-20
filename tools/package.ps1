<#
.SYNOPSIS
    Builds a release of Sea Power Radio and stages the folder that goes to the Steam Workshop.

.DESCRIPTION
    Release builds without the automatic deploy, then dist\SeaPowerRadio\ with exactly what players get:
    SeaPowerRadio.dll (the 3D-globe game) and SeaPowerRadioFlat.dll (Sea Power 0.8.7's flat map), the same sources
    built against each game's assemblies, each staying off on the other game; _info.ini, rats-radio-aliases.txt (default hull prefixes), LICENSE.txt, CHANGELOG.md, CREDITS.txt (the voices' CC BY 4.0 attribution), voices\ - the
    catalog files and the rendered library from mod\voices\ (not in git; rendered with tools\voicegen) - and
    preview.jpg, the Workshop preview (workshop\preview.jpg, if present): the game's upload screen only browses
    StreamingAssets for it. Refuses to stage a library with personas missing.
    The flat build needs a copy of 0.8.7's Sea Power_Data\Managed (-FlatManagedDir; default: _gamerefs next to the mod
    folders in the workspace), the globe build the globe game's (-GlobeManagedDir when the installed game is 0.8.7).
    When the workspace's tools\compat-check is there, every DLL is checked against both games' assemblies: it must
    have nothing missing on its own game, and its entry point nothing missing on either. -NoFlat leaves the flat build out.
    -Install mirrors the staged folder into <GameDir>\Sea Power_Data\StreamingAssets\SeaPowerRadio\, the folder to
    pick in the game's Create Mod screen (Sea Power must be closed). -Zip also writes dist\SeaPowerRadio-<version>.zip
    for a GitHub release.
    The steps of the base are the shared ones of every R.A.T.S. mod (SeaPowerShared\tools\RatsPackage.psm1); the workspace
    is found above this repo, -Workspace names it for a clone elsewhere, -RatsShared another copy of the shared repo.
    -Pack <id> stages a voice pack instead of the base: the folder with that voicepack.ini Id (looked up in mod\voices and
    in packs\*, e.g. a pack under work) goes to dist\pack-<id>\ as it is (voicepack.ini, personas.txt, catalog.txt, names.txt, clips;
    plus the pack's own _info.ini and CREDITS if it has them) - the folder to upload to that pack's Workshop item. No
    build, no DLL: packs never carry code. -Zip also writes dist\SeaPowerRadio-pack-<id>-<version>.zip.
#>
param(
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { 'E:\Games\steamapps\common\Sea Power' }),
    [string]$Workspace = $env:RatsWorkspace,
    [string]$RatsShared = $env:RatsShared,
    [string]$FlatManagedDir = $env:FlatManagedDir,
    # The globe build's game assemblies when the installed game isn't the globe version (a saved copy of its Managed).
    [string]$GlobeManagedDir = $env:GlobeManagedDir,
    [switch]$NoFlat,
    [switch]$Install,
    [switch]$Zip,
    [string]$Pack
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Workspace) {
    $Workspace = $root
    while ($Workspace -and -not (Test-Path (Join-Path $Workspace 'mods.json'))) { $Workspace = Split-Path -Parent $Workspace }
    if (-not $Workspace) { throw 'No workspace (a folder with mods.json) above this repo - pass -Workspace.' }
}
if (-not $RatsShared) { $RatsShared = Join-Path $Workspace 'SeaPowerShared' }
Import-Module (Join-Path $RatsShared 'tools\RatsPackage.psm1') -Force
if (-not $FlatManagedDir) { $FlatManagedDir = Join-Path $Workspace '_gamerefs\SeaPower-0.8.7-public\Managed' }

$name = 'SeaPowerRadio'
$project = Join-Path $root "src\$name\$name.csproj"
$dist = Join-Path $root "dist\$name"
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

$env:RatsShared = $RatsShared.TrimEnd('\') + '\'      # the csproj's -p:RatsShared, also for the flat build
Build-RatsMod -Project $project -Dist $dist -GameDir $GameDir -GlobeManagedDir $globeManaged -FlatManagedDir $FlatManagedDir -Workspace $Workspace -NoFlat:$NoFlat | Out-Null
Test-RatsCompat -Workspace $Workspace -Dist $dist -RootNamespace $name -GlobeManagedDir $globeManaged -FlatManagedDir $FlatManagedDir
Copy-RatsCommon -Root $root -Dist $dist -InfoIni (Join-Path $root 'mod\_info.ini')
Copy-Item (Join-Path $root 'mod\rats-radio-aliases.txt') $dist
Copy-Item (Join-Path $root 'VOICES-LICENSE.md') (Join-Path $dist 'CREDITS.txt')

# robocopy is far faster than Copy-Item for ~57,000 small clips; half-written takes (*.part.mp3) stay behind.
# The mood takes (<persona>\stressed|happy|sad, docs/moods.md) travel with their voices.
robocopy $voices (Join-Path $dist 'voices') /E /XF *.part.mp3 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the voice library failed (robocopy $LASTEXITCODE)." }

$version = Get-RatsModVersion -PluginFile (Join-Path $root "src\$name\Plugin.cs")
$clips = (Get-ChildItem -Recurse -File -Filter *.mp3 (Join-Path $dist 'voices')).Count
Write-Host "Staged Sea Power Radio $version in $dist ($($personas.Count) voices, $clips clips)"
Show-RatsStage -Dist $dist
if ($Zip) { New-RatsZip -Dist $dist -Version $version }
if ($Install) { Install-RatsMod -Dist $dist -GameDir $GameDir }
