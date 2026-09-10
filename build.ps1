<#
.SYNOPSIS
    Сборка AIFramework в одном из двух вариантов: полная или лёгкая.

.DESCRIPTION
    Полная сборка (по умолчанию) — AIFramework.Full.sln: все библиотеки, модульные тесты,
    проекты всех решений (демо, примеры, GPU) и инструменты, с нативными библиотеками
    всех платформ.

    Лёгкая сборка (-Light) — AIFramework.Light.sln: все библиотеки src, кроме GPU,
    и модульные тесты. В выход попадают только нативные файлы своей платформы и нет
    нативного OpenBLAS: нейросети считают собственным управляемым умножением.
    Выходы лёгкой сборки лежат в bin-light и obj-light и не смешиваются с полной.

.PARAMETER Light
    Лёгкая сборка вместо полной.

.PARAMETER Test
    После сборки прогнать тесты выбранного варианта.

.PARAMETER Clean
    Удалить каталоги сборки обоих вариантов (bin, obj, bin-light, obj-light) и выйти.

.PARAMETER Configuration
    Конфигурация: Release (по умолчанию) или Debug.

.EXAMPLE
    .\build.ps1
    Полная сборка в Release.

.EXAMPLE
    .\build.ps1 -Light -Test
    Лёгкая сборка и её тесты.

.EXAMPLE
    .\build.ps1 -Clean
    Удалить все каталоги сборки.
#>
[CmdletBinding()]
param(
    [switch]$Light,
    [switch]$Test,
    [switch]$Clean,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$roots = @('src', 'Tests', 'Demo', 'Tools')
$outputNames = @('bin', 'obj', 'bin-light', 'obj-light')

# Каталоги сборки лежат рядом с файлом проекта, поэтому искать их нужно именно там:
# обход вглубь bin упирается в пути длиннее 260 символов и занимает минуты
function Get-ProjectDirectories {
    Get-ChildItem -Path $roots -Recurse -Filter *.csproj -File -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -notmatch '\\(bin|obj|bin-light|obj-light)(\\|$)' } |
        ForEach-Object { $_.DirectoryName } |
        Sort-Object -Unique
}

function Get-OutputSize([string[]]$names) {
    $total = 0L

    foreach ($directory in Get-ProjectDirectories) {
        foreach ($name in $names) {
            $path = Join-Path $directory $name

            if (Test-Path -LiteralPath $path) {
                $sum = (Get-ChildItem -LiteralPath $path -Recurse -File -Force -ErrorAction SilentlyContinue |
                    Measure-Object -Property Length -Sum).Sum

                if ($sum) { $total += $sum }
            }
        }
    }

    return $total
}

if ($Clean) {
    # Сборочные серверы держат файлы в obj открытыми — без остановки часть не удалится
    dotnet build-server shutdown | Out-Null
    $removed = 0

    foreach ($directory in Get-ProjectDirectories) {
        foreach ($name in $outputNames) {
            $path = Join-Path $directory $name

            if (Test-Path -LiteralPath $path) {
                # rd с префиксом \\?\ удаляет пути длиннее 260 символов,
                # на которых спотыкается Remove-Item в Windows PowerShell
                cmd /c "rd /s /q `"\\?\$path`"" | Out-Null
                $removed++
            }
        }
    }

    Write-Host "Удалено каталогов сборки: $removed"
    exit 0
}

$solution = if ($Light) { 'AIFramework.Light.sln' } else { 'AIFramework.Full.sln' }
$variant = if ($Light) { 'лёгкая' } else { 'полная' }
$arguments = @('-c', $Configuration, '-nologo')

if ($Light) { $arguments += '-p:AILightBuild=true' }

Write-Host "Сборка: $variant, $Configuration, $solution"
$watch = [System.Diagnostics.Stopwatch]::StartNew()

dotnet build $solution @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Test) {
    dotnet test $solution @arguments --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$names = if ($Light) { @('bin-light', 'obj-light') } else { @('bin', 'obj') }
$size = Get-OutputSize $names

Write-Host ("Готово за {0:mm\:ss}. Каталоги сборки ({1}): {2:N2} ГБ" -f $watch.Elapsed, ($names -join ', '), ($size / 1GB))
