[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$workspaceDir = $PSScriptRoot
$backendDir = Join-Path $workspaceDir "e-imza-backend"
$pidFile = Join-Path $workspaceDir ".run\e-imza-signer.pid"

if (Test-Path -LiteralPath $pidFile) {
    $savedPid = [int](Get-Content -LiteralPath $pidFile -Raw)
    $signerProcess = Get-Process -Id $savedPid -ErrorAction SilentlyContinue

    if ($null -ne $signerProcess) {
        Stop-Process -Id $savedPid -Force
        $signerProcess.WaitForExit(5000)
    }

    Remove-Item -LiteralPath $pidFile -Force
}

Push-Location $backendDir
try {
    & docker compose down
    if ($LASTEXITCODE -ne 0) {
        throw "Docker servisleri durdurulamadi."
    }
}
finally {
    Pop-Location
}

Write-Host "E-imza servisleri durduruldu. Veritabani ve MinIO verileri korundu."
