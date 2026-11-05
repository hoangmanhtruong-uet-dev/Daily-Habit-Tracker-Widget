param(
    [string]$Configuration = "Release",
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"

$rootDir = Split-Path -Parent $PSScriptRoot
$appProj = Join-Path $rootDir "src\HabitGrid.App\HabitGrid.App.csproj"
$packageDir = Join-Path $rootDir "src\HabitGrid.Package"
$outputBase = Join-Path $rootDir "AppPackages"
$layoutDir = Join-Path $outputBase "Layout"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  HabitGrid MSIX Packaging Automation     " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean previous build layout
if (Test-Path $layoutDir) {
    Remove-Item -Recurse -Force $layoutDir
}
New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null

# 2. Publish HabitGrid.App
Write-Host "[1/4] Publishing HabitGrid.App ($Configuration, $Architecture)..." -ForegroundColor Yellow
$appPublishDir = Join-Path $layoutDir "HabitGrid.App"
dotnet publish $appProj -c $Configuration -r "win-$Architecture" --self-contained false -o $appPublishDir

# 3. Copy Manifest and Visual Assets
Write-Host "[2/4] Assembling Package Layout..." -ForegroundColor Yellow
Copy-Item (Join-Path $packageDir "Package.appxmanifest") (Join-Path $layoutDir "AppxManifest.xml")
Copy-Item -Recurse (Join-Path $packageDir "Images") (Join-Path $layoutDir "Images")

# 4. Check for makeappx.exe
Write-Host "[3/4] Searching for makeappx.exe tool..." -ForegroundColor Yellow
$makeappx = (Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName)

$msixOut = Join-Path $outputBase "HabitGrid_1.0.0.0_$Architecture.msix"

if ($makeappx -and (Test-Path $makeappx)) {
    Write-Host "Found makeappx at: $makeappx" -ForegroundColor Green
    Write-Host "[4/4] Packing into MSIX: $msixOut..." -ForegroundColor Yellow
    & $makeappx pack /d $layoutDir /p $msixOut /o
    Write-Host "SUCCESS: MSIX package generated at: $msixOut" -ForegroundColor Green
} else {
    Write-Host "[4/4] Note: Windows SDK makeappx.exe not found in default location." -ForegroundColor Yellow
    Write-Host "The MSIX Layout folder is fully generated and ready at:" -ForegroundColor Green
    Write-Host "  $layoutDir" -ForegroundColor Cyan
    Write-Host "You can submit or pack with Windows 10/11 SDK or Partner Center directly." -ForegroundColor Gray
}

Write-Host "Done!" -ForegroundColor Green
