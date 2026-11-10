param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$compilerRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $compilerRoot
try {
    if (!$SkipBuild) { & ./service/runner/build.ps1 }
    Add-Type -AssemblyName System.IO.Compression
    $outputDirectory = Join-Path $PSScriptRoot 'output'
    New-Item -ItemType Directory -Force $outputDirectory | Out-Null
    $packagePath = Join-Path $outputDirectory 'demonstration.gwz'
    $packageStream = [System.IO.File]::Open($packagePath, [System.IO.FileMode]::Create)
    $packageZip = [System.IO.Compression.ZipArchive]::new($packageStream, [System.IO.Compression.ZipArchiveMode]::Create)
    $packageWriter = [System.IO.StreamWriter]::new($packageZip.CreateEntry('cartridge.lua').Open())
    $packageWriter.Write([System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'cartridge.lua')))
    $packageWriter.Dispose()
    $packageZip.Dispose()
    $packageStream.Dispose()
    $builds = @()
    foreach ($device in @('Garmin','iPhone','WhereYouGo')) {
        $filename = 'demonstration-' + $device + '.gwc'
        $gwc = Join-Path $outputDirectory $filename
        $report = Join-Path $outputDirectory ($device + '-result.json')
        & ./bin/Release/CompilerJob.exe $packagePath $device $gwc $report
        $result = Get-Content -Raw $report | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or !$result.ok) { throw ($device + ': ' + ($result.messages -join '; ')) }
        $bytes = [System.IO.File]::ReadAllBytes($gwc)
        if ([BitConverter]::ToString($bytes,0,7) -ne '02-0A-43-41-52-54-00') { throw 'Invalid GWC signature' }
        $chunkOffset = [BitConverter]::ToInt32($bytes,11)
        if ([BitConverter]::ToString($bytes,$chunkOffset+4,5) -ne '1B-4C-75-61-51') { throw 'Invalid Lua 5.1 bytecode' }
        $builds += [ordered]@{device=$device; filename=$filename; bytes=$bytes.Length; sha256=(Get-FileHash -LiteralPath $gwc -Algorithm SHA256).Hash.ToLowerInvariant()}
        Write-Output "PASS: approved $device demonstration compiled ($($bytes.Length) bytes)"
    }
    [ordered]@{title='Wherigo Compiler Demonstration'; source='demonstration.gwz'; builtAt=[DateTime]::UtcNow.ToString('o'); builds=$builds} |
        ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $outputDirectory 'manifest.json')
    $global:LASTEXITCODE = 0
} finally { Pop-Location }
