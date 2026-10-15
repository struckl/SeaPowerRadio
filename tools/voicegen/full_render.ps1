# Full Chatterbox render of the voice library, detached from any terminal.
#
# Waits until no other Chatterbox job (e.g. the Nuclear Option voice tools) has used the GPU for -QuietSeconds,
# then renders every persona. Resumable: rerun this script after an interruption and it continues where it stopped.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\voicegen\full_render.ps1
#
# Progress: tools\voicegen\out\full_render.log

param(
    [int]$QuietSeconds = 180,
    [string]$Voices = "all"
)

$repo = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$out = Join-Path $PSScriptRoot "out"
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out "full_render.log"
$errLog = Join-Path $out "full_render.err.log"

function Write-Log($msg) { "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $msg | Add-Content -Path $log -Encoding utf8 }

function Get-OtherGpuJobs {
    Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
        Where-Object { $_.CommandLine -match 'generate_chatter|generate_voices|chatterbox' -and
                       $_.CommandLine -notmatch 'chatterbox_render\.py --voices' }
}

Write-Log "waiting until no other GPU voice job has run for $QuietSeconds s"
$quietSince = $null
while ($true) {
    if (Get-OtherGpuJobs) { $quietSince = $null }
    elseif (-not $quietSince) { $quietSince = Get-Date }
    elseif (((Get-Date) - $quietSince).TotalSeconds -ge $QuietSeconds) { break }
    Start-Sleep -Seconds 10
}

Write-Log "starting full render (voices: $Voices)"
$env:TQDM_DISABLE = "1"
$env:PYTHONUNBUFFERED = "1"
$env:PYTHONIOENCODING = "utf-8"
Push-Location $repo
# cmd does the redirection: PowerShell 5.1 would re-encode native output as UTF-16 and wrap stderr in error records.
cmd /c "`"$repo\.venv-chatterbox\Scripts\python.exe`" -u tools\voicegen\chatterbox_render.py --voices $Voices >> `"$log`" 2>> `"$errLog`""
$code = $LASTEXITCODE
Pop-Location
Write-Log "full render finished with exit code $code"
