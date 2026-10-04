$scriptDir = $PSScriptRoot
$appSecretPath = Join-Path $scriptDir '.appSecret'
$appCenterSecretPath = Join-Path $scriptDir 'AppCenterSecret.cs'

if (-not (Test-Path -LiteralPath $appSecretPath -PathType Leaf)) {
    Write-Host 'No .appSecret present, ignoring...'
    exit 0
}

$appSecret = (Get-Content -LiteralPath $appSecretPath -Raw).Trim()
$execType = $args[0]

$appCenterSecretCs = Get-Content -LiteralPath $appCenterSecretPath -Encoding UTF8 -Raw

if ($execType -eq 'pre') {
    $appCenterSecretCs = $appCenterSecretCs.Replace('{app secret}', $appSecret)
}
elseif ($execType -eq 'post') {
    $appCenterSecretCs = $appCenterSecretCs.Replace($appSecret, '{app secret}')
}
else {
    Write-Error "Unknown execution type: '$execType'. Expected 'pre' or 'post'."
    exit 1
}

$appCenterSecretCs | Out-File -LiteralPath $appCenterSecretPath -Encoding UTF8 -NoNewline
exit 0
