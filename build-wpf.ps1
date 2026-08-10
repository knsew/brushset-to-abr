# WPF 版构建脚本
# 用法:
#   powershell -NoProfile -ExecutionPolicy Bypass -File build-wpf.ps1            # 调试构建
#   powershell -NoProfile -ExecutionPolicy Bypass -File build-wpf.ps1 -Publish  # 发布自包含单文件 exe
# 产物:
#   调试构建 -> BrushStudioWpf\bin\Debug\net9.0-windows\win-x64\PNGtoABR.exe
#   发布构建 -> D:\ps\dist\PNGtoABR.exe（自包含单文件，可直接分发）

param([switch]$Publish)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $here 'BrushStudioWpf')

# 优先使用用户目录安装的 SDK（免管理员），其次系统 PATH
$dotnet = 'dotnet'
if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
}

if ($Publish) {
    $outDir = 'D:\ps\dist'
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    & $dotnet publish -c Release -o $outDir
    if ($LASTEXITCODE -ne 0) { Write-Host 'PUBLISH FAILED.' -ForegroundColor Red; exit $LASTEXITCODE }
    Write-Host ("PUBLISH OK -> {0}\PNGtoABR.exe" -f $outDir) -ForegroundColor Green
}
else {
    & $dotnet build -c Debug
    if ($LASTEXITCODE -ne 0) { Write-Host 'BUILD FAILED.' -ForegroundColor Red; exit $LASTEXITCODE }
    Write-Host 'BUILD OK.' -ForegroundColor Green
}
