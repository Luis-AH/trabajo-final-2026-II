param([string]$PublicBaseUrl = '')
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$privateDirectory = Join-Path $repositoryRoot '.demo'
$accountsPath = Join-Path $privateDirectory 'accounts.private.json'
if (-not (Test-Path -LiteralPath $accountsPath)) { throw 'Falta el archivo privado de las cuentas locales. No se generaron ni publicaron secretos.' }
$accounts = Get-Content -LiteralPath $accountsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($accounts.Count -ne 10 -or @($accounts | Where-Object { $_.Email -notlike '*@example.invalid' -or [string]::IsNullOrWhiteSpace($_.Password) }).Count) { throw 'El archivo debe contener las diez cuentas ficticias del catálogo.' }
$settings = [ordered]@{
    ASPNETCORE_ENVIRONMENT = 'Demo'
    PORT = '8080'
    ConnectionStrings__DefaultConnection = 'Data Source=/tmp/segunda-demo/app.db;Cache=Shared'
    DataProtection__KeyPath = '/tmp/segunda-demo/keys'
    SampleData__InitializeOnStartup = 'true'
    SampleData__CredentialsJson = ($accounts | ForEach-Object { [ordered]@{Email=[string]$_.Email;Password=[string]$_.Password} } | ConvertTo-Json -Compress)
}
$privateProject = [xml](Get-Content -LiteralPath (Join-Path $repositoryRoot 'SegundaOportunidad/SegundaOportunidad.csproj') -Raw)
$secretsId = [string]$privateProject.Project.PropertyGroup.UserSecretsId
$secretsPath = Join-Path $env:APPDATA ('Microsoft/UserSecrets/' + $secretsId + '/secrets.json')
if (Test-Path -LiteralPath $secretsPath) {
    $localSecrets = Get-Content -LiteralPath $secretsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in $localSecrets.PSObject.Properties) {
        if ($property.Name -match '^(Gemini|AdminGuard|Algolia|RabbitMQ|PieSocket|Redis|RecoveryEmail):' -and $property.Value -is [string] -and -not [string]::IsNullOrWhiteSpace($property.Value) -and $property.Name -ne 'RecoveryEmail:PublicBaseUrl') {
            $settings[$property.Name.Replace(':','__')] = [string]$property.Value
        }
    }
}
if ($PublicBaseUrl) {
    $publicUri = $null
    if (-not [Uri]::TryCreate($PublicBaseUrl,[UriKind]::Absolute,[ref]$publicUri) -or $publicUri.Scheme -ne 'https' -or $publicUri.UserInfo) { throw 'PublicBaseUrl debe ser HTTPS y no contener credenciales.' }
    $settings['RecoveryEmail__PublicBaseUrl'] = $PublicBaseUrl.TrimEnd('/')
}
$lines = foreach ($entry in $settings.GetEnumerator()) {
    $value = [string]$entry.Value
    if ($value.Contains("'") -or $value.Contains("`r") -or $value.Contains("`n")) { throw ('Configura manualmente la variable ' + $entry.Key + '; su formato no permite esta exportación segura.') }
    $entry.Key + "='" + $value + "'"
}
$environmentPath = Join-Path $privateDirectory 'render-presentation.private.env'
[IO.File]::WriteAllLines($environmentPath,$lines,(New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $privateDirectory 'render-accounts.private.json'),$settings['SampleData__CredentialsJson'],(New-Object Text.UTF8Encoding($false)))
Write-Output ('Preparadas ' + $settings.Count + ' variables en .demo/render-presentation.private.env, ignorado por Git y Docker. No se muestran secretos.')
if (-not $PublicBaseUrl) { Write-Output 'Para correo real configura RecoveryEmail__PublicBaseUrl con la URL HTTPS real del servicio. No se exportó el localhost.' }
Write-Output 'Importar únicamente en Render > Environment > Add from .env. Nunca subir estos archivos privados al repositorio.'
