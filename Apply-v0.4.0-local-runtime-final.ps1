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

Set-Location -LiteralPath $RepositoryPath

Write-Host "Restaurando la rama del PR #4..." -ForegroundColor Cyan
Invoke-Checked { git fetch origin } "git fetch"
Invoke-Checked { git checkout $Branch } "git checkout"
Invoke-Checked { git reset --hard "origin/$Branch" } "git reset"
Invoke-Checked { git clean -fd } "git clean"

Write-Host "Aplicando corrección final del motor local..." -ForegroundColor Cyan
Get-ChildItem -LiteralPath $PackageRoot -Force |
    Where-Object {
        $_.Name -ne "Apply-v0.4.0-local-runtime-final.ps1"
    } |
    Copy-Item -Destination $RepositoryPath -Recurse -Force

Write-Host "Restaurando dependencias..." -ForegroundColor Cyan
Invoke-Checked {
    dotnet restore BookTranslatorStudio.sln
} "dotnet restore"

Write-Host "Compilando Release..." -ForegroundColor Cyan
Invoke-Checked {
    dotnet build BookTranslatorStudio.sln -c Release --no-restore
} "dotnet build"

Invoke-Checked { git add -A } "git add"

$changes = git status --porcelain

if (-not [string]::IsNullOrWhiteSpace($changes)) {
    Invoke-Checked {
        git commit -m "Fix asynchronous Ollama download stream reading"
    } "git commit"

    Invoke-Checked {
        git push origin $Branch
    } "git push"
}

Write-Host ""
Write-Host "Motor local compilado y PR #4 actualizado correctamente." -ForegroundColor Green
