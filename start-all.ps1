[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$workspaceDir = $PSScriptRoot
$backendDir = Join-Path $workspaceDir "e-imza-backend"
$signerDir = Join-Path $workspaceDir "e-imza-net"
$signerProject = Join-Path $signerDir "EimzaSignerService.csproj"
$signerDll = Join-Path $signerDir "bin\Release\net8.0\EimzaSignerService.dll"
$stateDir = Join-Path $workspaceDir ".run"
$pidFile = Join-Path $stateDir "e-imza-signer.pid"
$stdoutLog = Join-Path $stateDir "e-imza-signer.stdout.log"
$stderrLog = Join-Path $stateDir "e-imza-signer.stderr.log"
$secretInitializer = Join-Path $workspaceDir "initialize-secrets.ps1"
$envFile = Join-Path $backendDir ".env"

New-Item -ItemType Directory -Path $stateDir -Force | Out-Null

& $secretInitializer
$secretValues = @{}
foreach ($line in Get-Content -LiteralPath $envFile) {
    if ($line -match '^([^#=]+)=(.*)$') {
        $secretValues[$matches[1]] = $matches[2]
    }
}

Write-Host "Docker servisleri baslatiliyor..."
Push-Location $backendDir
try {
    & docker compose up -d postgres minio
    if ($LASTEXITCODE -ne 0) {
        throw "Veritabani ve depolama servisleri baslatilamadi."
    }

    $postgresReady = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        & docker exec eimza-postgres pg_isready -U admin -d eimza_db 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $postgresReady = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $postgresReady) { throw "PostgreSQL hazir duruma gelmedi." }

    $databasePassword = $secretValues['POSTGRES_PASSWORD'].Replace("'", "''")
    $alterPasswordSql = "ALTER USER admin WITH PASSWORD '$databasePassword';"
    $alterPasswordSql | & docker exec -i eimza-postgres psql -U admin -d eimza_db | Out-Null
    $alterPasswordSql = $null
    $databasePassword = $null
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL parolasi guvenli degerle guncellenemedi." }

    & docker compose up --build -d
    if ($LASTEXITCODE -ne 0) {
        throw "Docker servisleri baslatilamadi."
    }
}
finally {
    Pop-Location
}

$signerIsRunning = $false
if (Test-Path -LiteralPath $pidFile) {
    $savedPid = [int](Get-Content -LiteralPath $pidFile -Raw)
    $savedProcess = Get-Process -Id $savedPid -ErrorAction SilentlyContinue
    $signerIsRunning = $null -ne $savedProcess
}

if ($signerIsRunning -and -not $SkipBuild) {
    Stop-Process -Id $savedPid -Force
    Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue
    $signerIsRunning = $false
}

if (-not $signerIsRunning) {
    if (-not $SkipBuild) {
        Write-Host ".NET imzalama servisi derleniyor..."
        & dotnet build $signerProject -c Release
        if ($LASTEXITCODE -ne 0) {
            throw ".NET imzalama servisi derlenemedi."
        }
    }

    if (-not (Test-Path -LiteralPath $signerDll)) {
        throw "Derlenmis imzalama servisi bulunamadi: $signerDll"
    }

    $previousUrls = $env:ASPNETCORE_URLS
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $previousSignerApiKey = $env:SIGNER_API_KEY
    try {
        # Yalnizca yerel makineye acilir. Docker Desktop, host.docker.internal
        # uzerinden bu adrese erisim saglar.
        $env:ASPNETCORE_URLS = "http://127.0.0.1:5194"
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:SIGNER_API_KEY = $secretValues['SIGNER_API_KEY']

        $signerProcess = Start-Process `
            -FilePath "dotnet" `
            -ArgumentList @($signerDll) `
            -WorkingDirectory $signerDir `
            -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutLog `
            -RedirectStandardError $stderrLog `
            -PassThru

        Set-Content -LiteralPath $pidFile -Value $signerProcess.Id -NoNewline
    }
    finally {
        $env:ASPNETCORE_URLS = $previousUrls
        $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
        $env:SIGNER_API_KEY = $previousSignerApiKey
    }
}

# Sirlar artik bu baslatma surecinde gerekli degil. Bellekte tutulma suresini kisalt.
$secretValues.Clear()
$secretValues = $null

$ready = $false
for ($attempt = 1; $attempt -le 30; $attempt++) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connection = $client.BeginConnect("127.0.0.1", 5194, $null, $null)
        $ready = $connection.AsyncWaitHandle.WaitOne(1000) -and $client.Connected
    }
    catch {
        $ready = $false
    }
    finally {
        $client.Dispose()
    }

    if ($ready) { break }
    Start-Sleep -Seconds 1
}

if (-not $ready) {
    throw ".NET imzalama servisi 5194 portunda baslamadi. Log: $stderrLog"
}

Write-Host ""
Write-Host "Tum servisler calisiyor:"
Write-Host "  Kullanici UI:  http://localhost:3000"
Write-Host "  Backend:       http://localhost:5050 (yalnizca bu bilgisayar)"
Write-Host "  Imza servisi:  http://localhost:5194/api/sign"
Write-Host "  MinIO:         http://localhost:9001"
Write-Host ""
Write-Host "Durdurmak icin: .\stop-all.ps1"
