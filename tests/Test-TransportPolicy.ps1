param(
    [Parameter(Mandatory = $true)]
    [string]$ServerDll,
    [Parameter(Mandatory = $true)]
    [string]$TestRoot,
    [int]$Port = 5299
)

$ErrorActionPreference = 'Stop'
$ServerDll = [System.IO.Path]::GetFullPath($ServerDll)
$TestRoot = [System.IO.Path]::GetFullPath($TestRoot)
if (-not (Test-Path -LiteralPath $ServerDll -PathType Leaf)) {
    throw 'A DLL do servidor nao existe.'
}
New-Item -ItemType Directory -Force -Path $TestRoot, (Join-Path $TestRoot 'admin-keys') | Out-Null

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$integrityKey = New-Object byte[] 32
$secretKey = New-Object byte[] 32
try {
    $rng.GetBytes($integrityKey)
    $rng.GetBytes($secretKey)
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
    $env:TURBORAMA_SERVER_STATE_FILE = Join-Path $TestRoot 'state.json'
    $env:TURBORAMA_SERVER_STATE_KEY = [Convert]::ToBase64String($integrityKey)
    $env:TURBORAMA_SERVER_SECRET_KEY = [Convert]::ToBase64String($secretKey)
    $env:TURBORAMA_ALLOW_HTTP_LOOPBACK = 'true'
    $env:TURBORAMA_ADMIN_USERNAME = ''
    $env:TURBORAMA_ADMIN_PASSWORD_HASH = ''
    $env:TURBORAMA_ADMIN_PUBLIC_HOST = ''
    $env:TURBORAMA_ADMIN_KEY_DIRECTORY = Join-Path $TestRoot 'admin-keys'

    $stdout = Join-Path $TestRoot 'stdout.log'
    $stderr = Join-Path $TestRoot 'stderr.log'
    $process = Start-Process -FilePath 'dotnet.exe' -ArgumentList @($ServerDll) `
        -WorkingDirectory $TestRoot -WindowStyle Hidden -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr -PassThru
    try {
        $ready = $false
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            Start-Sleep -Milliseconds 250
            $code = curl.exe --silent --output NUL --write-out '%{http_code}' --max-time 2 `
                "http://127.0.0.1:$Port/v1/health"
            if ($code -eq '200') { $ready = $true; break }
            if ($process.HasExited) { break }
        }
        if (-not $ready) {
            Get-Content -LiteralPath $stderr -ErrorAction SilentlyContinue
            throw 'O servidor de teste nao ficou pronto.'
        }

        $local = curl.exe --silent --output NUL --write-out '%{http_code}' --max-time 5 `
            "http://127.0.0.1:$Port/v1/health"
        $publicHttp = curl.exe --silent --output NUL --write-out '%{http_code}' --max-time 5 `
            -H 'Host: pix.lzgames.com.br' -H 'X-Forwarded-Proto: http' `
            "http://127.0.0.1:$Port/v1/health"
        $publicHttpsHeaders = curl.exe --silent --dump-header - --output NUL --max-time 5 `
            -H 'Host: pix.lzgames.com.br' -H 'X-Forwarded-Proto: https' `
            "http://127.0.0.1:$Port/v1/health"
        $publicHttpsStatus = $publicHttpsHeaders | Select-Object -First 1
        $hsts = @($publicHttpsHeaders | Where-Object { $_ -match '^Strict-Transport-Security:' })
        $apiAdmin = curl.exe --silent --output NUL --write-out '%{http_code}' --max-time 5 `
            -H 'Host: pix.lzgames.com.br' -H 'X-Forwarded-Proto: https' `
            "http://127.0.0.1:$Port/admin"

        Write-Output "LOCAL_HTTP=$local"
        Write-Output "PUBLIC_HOST_HTTP=$publicHttp"
        Write-Output "PUBLIC_FORWARDED_HTTPS_STATUS=$publicHttpsStatus"
        Write-Output "HSTS_COUNT=$($hsts.Count)"
        Write-Output "API_ADMIN=$apiAdmin"

        $accepted = $local -eq '200' -and $publicHttp -eq '400' -and `
            $publicHttpsStatus -match ' 200 ' -and $hsts.Count -eq 1 -and $apiAdmin -eq '404'
        if (-not $accepted) {
            throw 'A politica de transporte nao passou no teste HTTP real.'
        }
    }
    finally {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
    }
}
finally {
    $rng.Dispose()
    [Array]::Clear($integrityKey, 0, $integrityKey.Length)
    [Array]::Clear($secretKey, 0, $secretKey.Length)
    Remove-Item Env:TURBORAMA_SERVER_STATE_KEY -ErrorAction SilentlyContinue
    Remove-Item Env:TURBORAMA_SERVER_SECRET_KEY -ErrorAction SilentlyContinue
}
