param(
    [string]$RepositoryPath = "$HOME\Documents\Book-Translator-Studio"
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [scriptblock]$Command,
        [string]$Operation
    )

    & $Command

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation falló con código $LASTEXITCODE."
    }
}

$PackageRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$RepositoryPath = [IO.Path]::GetFullPath($RepositoryPath)
$Branch = "agent/unrestricted-translation-v0.4.0"

if (-not (Test-Path -LiteralPath (Join-Path $RepositoryPath ".git"))) {
    throw "Repositorio no encontrado: $RepositoryPath"
}

if ($PackageRoot.StartsWith(
        $RepositoryPath.TrimEnd('\') + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "La actualización debe estar fuera del repositorio."
}

Set-Location -LiteralPath $RepositoryPath

Write-Host "Restaurando la base estable..." -ForegroundColor Cyan
Invoke-Checked { git fetch origin } "git fetch"
Invoke-Checked { git checkout main } "git checkout main"
Invoke-Checked { git reset --hard origin/main } "git reset"
Invoke-Checked { git clean -fd } "git clean"

$localBranch = git branch --list $Branch
if (-not [string]::IsNullOrWhiteSpace($localBranch)) {
    Invoke-Checked {
        git branch -D $Branch
    } "eliminación de rama local incompleta"
}

Write-Host "Creando la rama v0.4.0..." -ForegroundColor Cyan
Invoke-Checked {
    git checkout -b $Branch
} "creación de rama"

Write-Host "Aplicando la entrega completa..." -ForegroundColor Cyan
Get-ChildItem -LiteralPath $PackageRoot -Force |
    Where-Object { $_.Name -ne "Apply-v0.4.0.ps1" } |
    Copy-Item -Destination $RepositoryPath -Recurse -Force

Write-Host "Restaurando dependencias..." -ForegroundColor Cyan
Invoke-Checked {
    dotnet restore BookTranslatorStudio.sln
} "dotnet restore"

Write-Host "Compilando Release..." -ForegroundColor Cyan
Invoke-Checked {
    dotnet build BookTranslatorStudio.sln -c Release --no-restore
} "dotnet build"

Write-Host "Publicando la versión..." -ForegroundColor Cyan
Invoke-Checked { git add -A } "git add"
Invoke-Checked {
    git commit -m "Add unrestricted translation workflow v0.4.0"
} "git commit"
Invoke-Checked {
    git push -u origin $Branch --force-with-lease
} "git push"

$pr = gh pr list `
    --head $Branch `
    --base main `
    --state open `
    --json number `
    --jq '.[0].number'

if ([string]::IsNullOrWhiteSpace($pr)) {
    Invoke-Checked {
        gh pr create `
            --draft `
            --base main `
            --head $Branch `
            --title "Add unrestricted translation workflow v0.4.0" `
            --body "Implementa traducción completa sin límites internos: perfiles abiertos, protocolos configurables, idiomas libres, alcance seleccionable, concurrencia controlada por el usuario, reintentos finitos o indefinidos, pausa, reanudación y autoguardado por bloque."
    } "creación del Pull Request"
}
else {
    Write-Host "PR existente actualizado: #$pr" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "v0.4.0 sin restricciones compilada y publicada correctamente." -ForegroundColor Green
