[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$envPath = Join-Path $PSScriptRoot "e-imza-backend\.env"

if (Test-Path -LiteralPath $envPath) {
    return
}

function New-RandomBase64([int]$ByteCount, [switch]$UrlSafe) {
    $bytes = [byte[]]::new($ByteCount)
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) }
    finally { $generator.Dispose() }
    $value = [Convert]::ToBase64String($bytes)
    if ($UrlSafe) {
        return $value.TrimEnd('=').Replace('+', '-').Replace('/', '_')
    }
    return $value
}

$lines = @(
    "POSTGRES_PASSWORD=$(New-RandomBase64 36 -UrlSafe)"
    "MINIO_ROOT_USER=eimza-storage-admin"
    "MINIO_ROOT_PASSWORD=$(New-RandomBase64 36 -UrlSafe)"
    "JWT_SECRET=$(New-RandomBase64 64)"
    "SIGNER_API_KEY=$(New-RandomBase64 48 -UrlSafe)"
)

[System.IO.File]::WriteAllLines($envPath, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Host "Guvenli servis anahtarlari olusturuldu: e-imza-backend\.env"
