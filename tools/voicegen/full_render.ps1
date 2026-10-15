# Full Chatterbox render of the voice library, detached and game-aware.
#
# Runs chatterbox_render.py --voices all in a hidden process and supervises it:
#   * a game is running (-Games)  -> the render is stopped, which also frees its 3-4 GB of video memory
#   * no game for -ResumeAfter s  -> the render is started again and continues where it stopped
#   * the render finishes         -> the supervisor exits
# Stopping is safe: clips are written to *.part.mp3 and renamed when complete, so at most the take in progress
# is lost. Rerun this script any time to resume.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\voicegen\full_render.ps1
#
# Log: tools\voicegen\out\full_render.log (render output: full_render.err.log for warnings)

param(
    [string[]]$Games = @("HuntGame", "Sea Power", "NuclearOption"),
    [int]$ResumeAfter = 120,
    [string]$Voices = "all"
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

function Start-Render {
    $env:TQDM_DISABLE = "1"
    $env:PYTHONUNBUFFERED = "1"
    $env:PYTHONIOENCODING = "utf-8"
    # cmd does the redirection: PowerShell 5.1 would re-encode native output as UTF-16.
    $cmdLine = "/c `"`"$python`" -u tools\voicegen\chatterbox_render.py --voices $Voices >> `"$log`" 2>> `"$errLog`"`""
    $p = Start-Process -FilePath "cmd.exe" -ArgumentList $cmdLine -WorkingDirectory $repo -WindowStyle Hidden -PassThru
    $null = $p.Handle  # keep a handle so ExitCode is available after exit
    $p
}

function Stop-Render($proc) {
    if ($proc -and -not $proc.HasExited) {
        & taskkill.exe /PID $proc.Id /T /F | Out-Null
        $proc.WaitForExit(15000) | Out-Null
    }
}

Write-Log "supervisor started (pid $PID), pausing for: $($Games -join ', ')"
$render = $null
$quietSince = $null
$failures = 0
$started = $false
$pausedFor = $null

while ($true) {
    $game = Get-RunningGame
    $running = $render -and -not $render.HasExited

    if ($game) {
        $quietSince = $null
        if ($running) { Stop-Render $render }
        if ($pausedFor -ne $game) { Write-Log "paused: $game is running"; $pausedFor = $game }
        $render = $null
    }
    elseif ($running) {
        # rendering, nothing to do
    }
    elseif ($render -and $render.HasExited) {
        $code = $render.ExitCode
        $render = $null
        if ($code -eq 0) {
            Write-Log "full render finished"
            break
        }
        $failures++
        Write-Log "render exited with code $code (failure $failures)"
        if ($failures -ge 5) { Write-Log "giving up after 5 failures"; break }
        Start-Sleep -Seconds 60
    }
    else {
        if (-not $quietSince) { $quietSince = Get-Date }
        if (-not $started -or ((Get-Date) - $quietSince).TotalSeconds -ge $ResumeAfter) {
            $render = Start-Render
            $started = $true
            $pausedFor = $null
            Write-Log "rendering (cmd pid $($render.Id))"
            $quietSince = $null
        }
    }
    Start-Sleep -Seconds 10
}
