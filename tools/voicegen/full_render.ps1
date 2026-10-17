# Full Chatterbox render of the voice library, detached and game-aware.
#
# 1. Renders chatterbox_render.py --voices all in a hidden process and supervises it:
#      a game is running (-Games)   -> the render is stopped, which also frees its 3-4 GB of video memory
#      no game for -ResumeAfter s   -> the render is started again and continues where it stopped
#    Stopping is safe: clips are written to *.part.mp3 and renamed when complete.
# 2. Checks the library (check_library.py). Takes that gave up get one more pass with new seeds.
# 3. Deploys the mod incl. voices into the game (dotnet build -> StreamingAssets\SeaPowerRadio).
# 4. With -ShutdownWhenDone: shuts the PC down after a warning (abort with: shutdown /a).
#
# Start it so it survives the terminal / app that launched it (WMI creates it outside the caller's job):
#   Invoke-CimMethod Win32_Process -MethodName Create -Arguments @{ CommandLine =
#     'powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "<repo>\tools\voicegen\full_render.ps1" -ShutdownWhenDone' }
# Rerun any time to resume. Log: tools\voicegen\out\full_render.log (warnings: full_render.err.log)

param(
    [string[]]$Games = @("HuntGame", "Sea Power", "NuclearOption", "cs2"),
    [int]$ResumeAfter = 120,
    [string]$Voices = "all",
    [string]$RenderArgs = "",          # extra chatterbox_render.py arguments, e.g. "--keys-file tools/voicegen/rerender_keys.txt"
    [switch]$ShutdownWhenDone,
    [int]$ShutdownDelay = 300
)

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$out = Join-Path $PSScriptRoot "out"
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out "full_render.log"
$errLog = Join-Path $out "full_render.err.log"
$python = Join-Path $repo ".venv-chatterbox\Scripts\python.exe"

function Write-Log($msg) { "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $msg | Add-Content -Path $log -Encoding utf8 }

function Get-RunningGame {
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $Games -contains $_.ProcessName } |
        Select-Object -First 1 -ExpandProperty ProcessName
}

function Start-Hidden($arguments) {
    $env:TQDM_DISABLE = "1"
    $env:PYTHONUNBUFFERED = "1"
    $env:PYTHONIOENCODING = "utf-8"
    # cmd does the redirection: PowerShell 5.1 would re-encode native output as UTF-16.
    $cmdLine = "/c `"$arguments >> `"$log`" 2>> `"$errLog`"`""
    $p = Start-Process -FilePath "cmd.exe" -ArgumentList $cmdLine -WorkingDirectory $repo -WindowStyle Hidden -PassThru
    $null = $p.Handle  # keep a handle so ExitCode is available after exit
    $p
}

function Stop-Tree($proc) {
    if ($proc -and -not $proc.HasExited) {
        & taskkill.exe /PID $proc.Id /T /F | Out-Null
        $proc.WaitForExit(15000) | Out-Null
    }
}

# Runs the render until it exits cleanly, pausing while games run. Returns $true on success.
function Invoke-SupervisedRender([string]$extra) {
    $render = $null; $quietSince = $null; $failures = 0; $started = $false; $pausedFor = $null
    while ($true) {
        $game = Get-RunningGame
        $running = $render -and -not $render.HasExited
        if ($game) {
            $quietSince = $null
            if ($running) { Stop-Tree $render }
            if ($pausedFor -ne $game) { Write-Log "paused: $game is running"; $pausedFor = $game }
            $render = $null
        }
        elseif ($running) { }
        elseif ($render -and $render.HasExited) {
            $code = $render.ExitCode
            $render = $null
            if ($code -eq 0) { return $true }
            $failures++
            Write-Log "render exited with code $code (failure $failures)"
            if ($failures -ge 5) { Write-Log "giving up after 5 failures"; return $false }
            Start-Sleep -Seconds 60
        }
        else {
            if (-not $quietSince) { $quietSince = Get-Date }
            if (-not $started -or ((Get-Date) - $quietSince).TotalSeconds -ge $ResumeAfter) {
                $render = Start-Hidden "`"$python`" -u tools\voicegen\chatterbox_render.py --voices $Voices $extra"
                $started = $true; $pausedFor = $null; $quietSince = $null
                Write-Log "rendering $extra(cmd pid $($render.Id))"
            }
        }
        Start-Sleep -Seconds 10
    }
}

function Test-Library {
    $p = Start-Hidden "`"$python`" tools\voicegen\check_library.py"
    $p.WaitForExit()
    return $p.ExitCode -eq 0
}

Write-Log "supervisor started (pid $PID), pausing for: $($Games -join ', '), shutdown when done: $ShutdownWhenDone"

$ok = Invoke-SupervisedRender "$RenderArgs "
$complete = $ok -and (Test-Library)
if ($ok -and -not $complete) {
    Write-Log "library incomplete, retry pass with new seeds"
    $ok = Invoke-SupervisedRender "$RenderArgs --seed-offset 1 "
    $complete = $ok -and (Test-Library)
}
Write-Log ("render done: success=$ok, library complete=$complete")

if ($ok) {
    while (Get-RunningGame) { Start-Sleep -Seconds 30 }   # the game would lock the plugin DLL
    $deploy = Start-Hidden "dotnet build src\SeaPowerRadio\SeaPowerRadio.csproj -c Release"
    $deploy.WaitForExit()
    Write-Log "deployed to the game folder (dotnet exit code $($deploy.ExitCode))"
}

if ($ShutdownWhenDone) {
    Write-Log "shutting down in $ShutdownDelay s (abort: shutdown /a)"
    & shutdown.exe /s /t $ShutdownDelay /c "Sea Power Radio: voice render finished. Shutting down in $([int]($ShutdownDelay / 60)) min - abort with: shutdown /a"
}
Write-Log "supervisor finished"
