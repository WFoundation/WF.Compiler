$ErrorActionPreference = 'Stop'
$compilerRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$outputDirectory = Join-Path $compilerRoot 'bin/Release'
Push-Location $compilerRoot
try {
    # Both are self contained win-x86 apps (lua5.1.dll is 32-bit), published into one folder
    & dotnet publish 'WF.Compiler.csproj' -c Release -o $outputDirectory --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Compiler build failed.' }
    & dotnet publish 'service/runner/CompilerJob.csproj' -c Release -o $outputDirectory --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Compiler service adapter build failed.' }
} finally { Pop-Location }
