$dotnetDir = Join-Path $env:USERPROFILE 'dotnet9'
$env:DOTNET_ROOT = $dotnetDir
$env:PATH = "$dotnetDir;$env:PATH"

Set-Location -Path $PSScriptRoot

Get-Process *MovaFlow* -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "=== 1/3. Publishing MovaFlow Portable (Standalone, ~67 MB) ==="
dotnet publish MovaFlow.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish_portable

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed at Portable stage with code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Copy-Item "publish_portable\MovaFlow.exe" "MovaFlow.exe" -Force
Copy-Item "publish_portable\MovaFlow.exe" "MovaFlow-Portable.exe" -Force
Write-Host "MovaFlow.exe and MovaFlow-Portable.exe ready!"

Write-Host "=== 2/3. Publishing MovaFlow Lite (Framework-Dependent, ~2.8 MB) ==="
dotnet publish MovaFlow.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish_lite

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed at Lite stage with code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Copy-Item "publish_lite\MovaFlow.exe" "MovaFlow-Lite.exe" -Force
Write-Host "MovaFlow-Lite.exe ready!"

Write-Host "=== 3/3. Building MovaFlow-Setup.exe (Dual Payload: Portable + Lite) ==="
Set-Location -Path (Join-Path $PSScriptRoot "installer")
dotnet publish MovaFlow-Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish_setup

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed at Setup stage with code $LASTEXITCODE"
    Set-Location -Path $PSScriptRoot
    exit $LASTEXITCODE
}

Copy-Item "publish_setup\MovaFlow-Setup.exe" "..\MovaFlow-Setup.exe" -Force
Set-Location -Path $PSScriptRoot
Write-Host "MovaFlow-Setup.exe ready!"

Write-Host "Cleaning temporary build directories..."
$dirsToClean = @('publish_portable', 'publish_lite', 'bin', 'obj', 'installer\publish_setup', 'installer\bin', 'installer\obj')
foreach ($d in $dirsToClean) {
    $p = Join-Path $PSScriptRoot $d
    if (Test-Path -LiteralPath $p) {
        Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "All targets built successfully:"
Get-Item 'MovaFlow.exe', 'MovaFlow-Portable.exe', 'MovaFlow-Lite.exe', 'MovaFlow-Setup.exe' | Format-Table Name, Length, LastWriteTime
