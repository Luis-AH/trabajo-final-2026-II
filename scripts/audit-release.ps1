$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repositoryRoot
try {
    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'feature/reciclaje-ronaldo') { throw 'Ejecuta esta auditoría únicamente en feature/reciclaje-ronaldo.' }
    $publishPath = Join-Path $repositoryRoot '.demo/publish-sin-maps'
    & dotnet publish SegundaOportunidad/SegundaOportunidad.csproj -c Release --no-restore -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish. No se inicia ninguna aplicación.' }
    $required = @('SegundaOportunidad.dll', 'App_Data/recommender.json', 'App_Data/demo-corpus.json', 'wwwroot/css/experience.css', 'wwwroot/js/recommendations.js', 'wwwroot/lib/bootstrap-icons/fonts/bootstrap-icons.woff2')
    $required += @('computacion','ropa','muebles','deportes','libros','oficina','celulares','electrodomesticos') | ForEach-Object { 'wwwroot/images/catalog/' + $_ + '.jpg' }
    $required += 'wwwroot/images/catalog/sources.json'
    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishPath $relative) -PathType Leaf)) { throw ('Falta recurso publicado: ' + $relative) }
    }
    $files = @(Get-ChildItem -LiteralPath $publishPath -Recurse -File)
    $forbidden = @($files | Where-Object { $_.Name -match '(?i)(\.db(?:-wal|-shm)?$|\.sqlite(?:3)?$|\.joblib$|^\.env(?:\.|$)|^secrets\.json$|^accounts\.private\.json$|^appsettings\.Development\.json$|^python(?:3\d*)?\.(?:exe|dll)$)' })
    if ($forbidden.Count -gt 0) { throw ('Archivos privados/locales inesperados en publicación: ' + (($forbidden | ForEach-Object { $_.Name }) -join ', ')) }
    $sourceHash = (Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'SegundaOportunidad/App_Data/recommender.json') -Algorithm SHA256).Hash
    $publishedHash = (Get-FileHash -LiteralPath (Join-Path $publishPath 'App_Data/recommender.json') -Algorithm SHA256).Hash
    if ($sourceHash -ne $publishedHash) { throw 'El modelo publicado no coincide con el entrenado.' }
    $blueprint = Get-Content -LiteralPath (Join-Path $repositoryRoot 'render.yaml') -Raw
    if ($blueprint -notmatch '(?m)^\s+plan:\s+free\s*$' -or $blueprint -match '(?m)^\s+disk:') { throw 'El Blueprint principal no corresponde a la demo free sin disco.' }
    $sizeMiB = [Math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
    Write-Output ('OK RELEASE: ' + $required.Count + ' recursos requeridos, sin archivos privados conocidos, modelo SHA256 ' + $sourceHash)
    Write-Output ('Tamaño de archivos publicados: ' + $sizeMiB + ' MiB. No es una medida de RAM ni prueba de Docker/Linux.')
    Write-Output 'No se migró app.db, no se inició la web ni se desplegó en Render.'
}
finally { Pop-Location }
