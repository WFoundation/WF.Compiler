$ErrorActionPreference = 'Stop'
$compilerRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $compilerRoot
try {
    & ./service/runner/build.ps1
    Add-Type -AssemblyName System.IO.Compression
    $outputDirectory = Join-Path $PSScriptRoot 'output'
    New-Item -ItemType Directory -Force $outputDirectory | Out-Null
    function Write-TestPackage([string]$name, [string]$lua) {
        $packagePath = Join-Path $outputDirectory ($name + '.gwz')
        $packageStream = [System.IO.File]::Open($packagePath, [System.IO.FileMode]::Create)
        $packageZip = [System.IO.Compression.ZipArchive]::new($packageStream, [System.IO.Compression.ZipArchiveMode]::Create)
        $packageWriter = [System.IO.StreamWriter]::new($packageZip.CreateEntry('cartridge.lua').Open())
        $packageWriter.Write($lua)
        $packageWriter.Dispose()
        $packageZip.Dispose()
        $packageStream.Dispose()
        return $packagePath
    }
    # These tests execute only the trusted Lua below. Never use this command on
    # user uploads: those must run in the network-isolated container.
    $trustedLua = 'require "Wherigo"; local cart = Wherigo.ZCartridge(); cart.Id = "0e8bc5ec-fdf1-44f5-b17a-7985e9861b69"; cart.Name = "Compiler smoke test"; cart.Description = "Trusted test fixture"; cart.Activity = "Tour guide"; cart.Author = "Wherigo"; cart.StartingLocation=false; cart.Media=false; cart.Icon=false; return cart'
    $sourcePackage = Write-TestPackage 'valid' $trustedLua
    $devices = @('Emulator','Garmin','iPad','iPhone','OpenWIG','PPC2003','WFPlayer','WhereYouGo','XMarksTheSpot')
    foreach ($device in $devices) {
        $gwc = Join-Path $outputDirectory ($device + '.gwc')
        $report = Join-Path $outputDirectory ($device + '.json')
        & ./bin/Release/CompilerJob.exe $sourcePackage $device $gwc $report
        $result = Get-Content -Raw $report | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or !$result.ok) { throw ($device + ': ' + ($result.messages -join '; ')) }
        $bytes = [System.IO.File]::ReadAllBytes($gwc)
        if ([BitConverter]::ToString($bytes,0,7) -ne '02-0A-43-41-52-54-00') { throw 'Invalid GWC signature' }
        $chunkOffset = [BitConverter]::ToInt32($bytes,11)
        if ([BitConverter]::ToString($bytes,$chunkOffset+4,5) -ne '1B-4C-75-61-51') { throw 'Invalid Lua 5.1 bytecode' }
        Write-Output "PASS: $device GWC header and Lua bytecode"
    }
    foreach ($lua in @('this is invalid lua', "local value = 1`nif true then")) {
        $badSource = Write-TestPackage 'invalid' $lua
        $report = Join-Path $outputDirectory 'invalid.json'
        & ./bin/Release/CompilerJob.exe $badSource 'Garmin' (Join-Path $outputDirectory 'invalid.gwc') $report
        $result = Get-Content -Raw $report | ConvertFrom-Json
        if ($LASTEXITCODE -eq 0 -or $result.ok -or $result.messages[0] -notmatch 'line') { throw 'Lua error diagnostics failed' }
        if (($result.messages -join ' ') -match 'Index was outside') { throw 'Lua diagnostic indexing regressed' }
        Write-Output 'PASS: Lua syntax error retains line diagnostics'
    }
    # Expected negative tests return 1 from the adapter. GitHub's pwsh wrapper
    # otherwise propagates that last native exit code despite successful checks.
    $global:LASTEXITCODE = 0
} finally { Pop-Location }
