<#
.SYNOPSIS
    Выкладывает пакеты из artifacts/nuget в порядке зависимостей.

.DESCRIPTION
    Каждый пакет уходит после тех, от которых зависит: тот, кто устанавливает пакет во время
    выкладки, не встретит ссылку на еще не опубликованную версию. Уже опубликованные версии
    пропускаются, поэтому после сбоя скрипт можно запустить заново.

    Выложенную на nuget.org версию нельзя удалить, только скрыть. Перед настоящей выкладкой
    стоит посмотреть порядок и состав через -DryRun.

    Ключ берется только из переменной окружения NUGET_API_KEY и не попадает в журнал.
    Обычно скрипт запускает рабочий процесс .github/workflows/publish-nuget.yml, который получает
    временный ключ через Trusted Publishing. Запуск с машины разработчика нужен только как запасной
    путь, если этот рабочий процесс недоступен.

.PARAMETER Path
    Каталог с пакетами, по умолчанию artifacts/nuget.

.PARAMETER Source
    Куда выкладывать: адрес источника NuGet или локальный каталог (для него ключ не нужен).

.PARAMETER DryRun
    Только проверить зависимости и показать порядок выкладки.

.EXAMPLE
    .\build\nuget-push.ps1 -DryRun
    Показать состав и порядок, ничего не выкладывая.

.EXAMPLE
    .\build\nuget-push.ps1 -Source C:\temp\feed
    Выложить в локальный каталог для проверки установки пакетов.
#>
[CmdletBinding()]
param(
    [string]$Path,
    [string]$Source = 'https://api.nuget.org/v3/index.json',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

# В Windows PowerShell 5.1 значение $PSScriptRoot пусто в блоке param
if (-not $Path) { $Path = Join-Path $PSScriptRoot '../artifacts/nuget' }

function Read-Package([string]$file) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($file)

    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        $reader = New-Object System.IO.StreamReader($entry.Open())
        $nuspec = [xml]$reader.ReadToEnd()
        $reader.Dispose()
    }
    finally {
        $archive.Dispose()
    }

    return [pscustomobject]@{
        Id           = $nuspec.package.metadata.id
        Version      = $nuspec.package.metadata.version
        File         = $file
        Dependencies = @($nuspec.SelectNodes("//*[local-name()='dependency']") | ForEach-Object { $_.GetAttribute('id') })
    }
}

$packages = @{}

foreach ($file in Get-ChildItem -LiteralPath $Path -Filter *.nupkg -File) {
    $package = Read-Package $file.FullName
    $packages[$package.Id] = $package
}

if ($packages.Count -eq 0) { throw "В каталоге $Path нет пакетов: сначала выполните .\build.ps1 -Pack" }

$ordered = New-Object System.Collections.Generic.List[object]
$visited = @{}

function Add-InDependencyOrder([string]$id) {
    if ($visited.ContainsKey($id)) { return }
    $visited[$id] = $true

    foreach ($dependency in $packages[$id].Dependencies) {
        if ($packages.ContainsKey($dependency)) {
            Add-InDependencyOrder $dependency
        }
        elseif ($dependency -match '^AI(\.|$)') {
            # Пакет сослался бы на версию, которой нет на nuget.org, и не устанавливался бы
            throw "Пакет $id зависит от $dependency, которого нет в каталоге $Path"
        }
    }

    $ordered.Add($packages[$id])
}

foreach ($id in $packages.Keys | Sort-Object) { Add-InDependencyOrder $id }

$arguments = @('--source', $Source, '--skip-duplicate')

if ($env:NUGET_API_KEY) {
    $arguments += @('--api-key', $env:NUGET_API_KEY)
}
elseif (-not $DryRun -and $Source -match '^https?://') {
    throw 'Не задана переменная окружения NUGET_API_KEY'
}

$number = 0

foreach ($package in $ordered) {
    $number++
    Write-Host ('[{0}/{1}] {2} {3}' -f $number, $ordered.Count, $package.Id, $package.Version)

    if ($DryRun) { continue }

    dotnet nuget push $package.File @arguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
