$ErrorActionPreference = 'Stop'
$compilerRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319'
Push-Location $compilerRoot
try {
    & (Join-Path $frameworkDirectory 'MSBuild.exe') 'WF.Compiler.csproj' '/p:Configuration=Release' '/p:Platform=x86' '/p:TargetFrameworkVersion=v4.7.2' '/verbosity:minimal' '/nologo'
    if ($LASTEXITCODE -ne 0) { throw 'Compiler build failed.' }
    & (Join-Path $frameworkDirectory 'csc.exe') '/nologo' '/target:exe' '/platform:x86' '/out:bin\Release\CompilerJob.exe' '/reference:bin\Release\WF.Compiler.exe' '/reference:System.Web.Extensions.dll' '/reference:System.IO.Compression.dll' 'service\runner\CompilerJob.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Compiler service adapter build failed.' }
} finally { Pop-Location }
