# build.ps1 - Сборка Hunt Loader
# Запускать из корня: .\build.ps1

param(
    [string]$Config = "Release",
    [switch]$Run,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$proj    = Join-Path $root "src\HuntLoader\HuntLoader.csproj"
$output  = Join-Path $root "bin\$Config"

Write-Host ""
Write-Host "╔══════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       Hunt Loader Build Script       ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Проверка .NET SDK
try {
    $dotnet = & dotnet --version 2>&1
    Write-Host "  [✓] .NET SDK: $dotnet" -ForegroundColor Green
} catch {
    Write-Host "  [✗] .NET SDK не найден!" -ForegroundColor Red
    Write-Host "  Скачай с: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Очистка
if ($Clean) {
    Write-Host "  [~] Очистка..." -ForegroundColor Yellow
    if (Test-Path $output) { Remove-Item $output -Recurse -Force }
    Write-Host "  [✓] Очищено" -ForegroundColor Green
}

# Восстановление пакетов
Write-Host "  [~] Восстановление NuGet пакетов..." -ForegroundColor Yellow
& dotnet restore $proj
if ($LASTEXITCODE -ne 0) { Write-Host "  [✗] Restore failed" -ForegroundColor Red; exit 1 }
Write-Host "  [✓] Пакеты восстановлены" -ForegroundColor Green

# Сборка
Write-Host "  [~] Сборка ($Config)..." -ForegroundColor Yellow
& dotnet build $proj -c $Config --no-restore -o $output
if ($LASTEXITCODE -ne 0) { Write-Host "  [✗] Build failed" -ForegroundColor Red; exit 1 }
Write-Host "  [✓] Сборка завершена!" -ForegroundColor Green
Write-Host "  [→] Вывод: $output" -ForegroundColor White

# Запуск
if ($Run) {
    Write-Host "  [~] Запуск..." -ForegroundColor Yellow
    $exe = Join-Path $output "HuntLoader.exe"
    if (Test-Path $exe) { Start-Process $exe }
    else { & dotnet run --project $proj -c $Config }
}

Write-Host ""
Write-Host "  Готово! " -ForegroundColor Cyan