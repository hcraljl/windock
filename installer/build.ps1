$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$innoCompiler = @("${env:LOCALAPPDATA}\Programs\Inno Setup 7\ISCC.exe", "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "${env:ProgramFiles}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $innoCompiler) { throw 'Inno Setup compiler not found.' }
Push-Location $taskRoot
try {
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File installer/make-logo.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Logo export failed.' }
    dotnet publish SuperDock/SuperDock.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/windock-win-x64 -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    & $innoCompiler installer/SuperDock.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
} finally { Pop-Location }

