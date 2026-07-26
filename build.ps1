param(
    [string]$GameRoot = 'S:\SteamLibrary\steamapps\common\THE HOUSE OF THE DEAD 2 Remake',
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'

$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer vswhere.exe not found.'
}

$visualStudio = & $vswhere -latest -products * `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath
if (-not $visualStudio) {
    throw 'Visual Studio C++ tools not found.'
}

$vcvars = Join-Path $visualStudio 'VC\Auxiliary\Build\vcvars64.bat'
$environment = & $env:ComSpec /d /c "call `"$vcvars`" >nul && set"
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to initialize Visual Studio x64 tools.'
}

foreach ($line in $environment) {
    if ($line -match '^([^=]+)=(.*)$' -and $matches[1] -cne 'Path') {
        Set-Item -LiteralPath "Env:$($matches[1])" -Value $matches[2]
    }
}

$cl = Join-Path $env:VCToolsInstallDir 'bin\Hostx64\x64\cl.exe'
if (-not (Test-Path -LiteralPath $cl)) {
    throw "MSVC compiler not found: $cl"
}

$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$obj = Join-Path $root 'obj'
New-Item -ItemType Directory -Force -Path $dist, $obj | Out-Null

$bridgeProject = Join-Path $root 'Hotd2TrainerBridge.csproj'
$bridgeOutput = Join-Path $dist 'Hotd2TrainerBridge.dll'
$bridgeBuild = Join-Path $obj 'bridge'

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

$trainerSource = Join-Path $root 'Hotd2RemakeTrainer.cpp'
$trainerOutput = Join-Path $dist 'Hotd2RemakeTrainer.exe'
$trainerObject = Join-Path $obj 'Hotd2RemakeTrainer.obj'

& $cl /nologo /std:c++17 /O2 /MT /W4 /EHsc `
    "/Fo:$trainerObject" "/Fe:$trainerOutput" $trainerSource `
    /link /SUBSYSTEM:WINDOWS /OPT:REF /OPT:ICF /MANIFEST:EMBED `
    user32.lib kernel32.lib shell32.lib gdi32.lib comctl32.lib `
    uxtheme.lib dwmapi.lib
if ($LASTEXITCODE -ne 0) {
    throw "Trainer build failed with exit code $LASTEXITCODE."
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
Write-Host "Built $bridgeOutput"
