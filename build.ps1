param(
    [string]$GameRoot = 'S:\SteamLibrary\steamapps\common\THE HOUSE OF THE DEAD 2 Remake',
    [switch]$Deploy,
    [switch]$SkipBridge
)

$ErrorActionPreference = 'Stop'

if ($Deploy -and $SkipBridge) {
    throw 'Cannot use -Deploy with -SkipBridge.'
}

$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$obj = Join-Path $root 'obj'
New-Item -ItemType Directory -Force -Path $dist, $obj | Out-Null

$trainerProject = Join-Path $root `
    'src\Hotd2RemakeTrainer.App\Hotd2RemakeTrainer.App.csproj'
$trainerTests = Join-Path $root `
    'tests\Hotd2RemakeTrainer.Tests\Hotd2RemakeTrainer.Tests.csproj'
$trainerBuild = Join-Path $obj 'trainer'
$trainerOutput = Join-Path $dist 'Hotd2RemakeTrainer.exe'

& dotnet run --project $trainerTests --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Trainer tests failed with exit code $LASTEXITCODE."
}

& dotnet publish $trainerProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $trainerBuild
if ($LASTEXITCODE -ne 0) {
    throw "Trainer publish failed with exit code $LASTEXITCODE."
}
Copy-Item -LiteralPath (Join-Path $trainerBuild 'Hotd2RemakeTrainer.exe') `
    -Destination $trainerOutput `
    -Force

$bridgeProject = Join-Path $root 'Hotd2TrainerBridge.csproj'
$bridgeOutput = Join-Path $dist 'Hotd2TrainerBridge.dll'
$bridgeBuild = Join-Path $obj 'bridge'

if (-not $SkipBridge) {
    & dotnet build $bridgeProject `
        --configuration Release `
        --output $bridgeBuild `
        "-p:GameRoot=$GameRoot"
    if ($LASTEXITCODE -ne 0) {
        throw "Bridge build failed with exit code $LASTEXITCODE."
    }
    Copy-Item -LiteralPath (Join-Path $bridgeBuild 'Hotd2TrainerBridge.dll') `
        -Destination $bridgeOutput `
        -Force
}
elseif (-not (Test-Path -LiteralPath $bridgeOutput)) {
    throw "Committed bridge binary not found: $bridgeOutput"
}

if ($Deploy) {
    $plugins = Join-Path $GameRoot 'BepInEx\plugins'
    New-Item -ItemType Directory -Force -Path $plugins | Out-Null
    Copy-Item -LiteralPath $bridgeOutput `
        -Destination (Join-Path $plugins 'Hotd2TrainerBridge.dll') `
        -Force
    Write-Host "Deployed bridge to $plugins"
}

Write-Host "Built $trainerOutput"
Write-Host "Ready $bridgeOutput"
