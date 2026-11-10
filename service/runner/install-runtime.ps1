# Explicit setup for disposable Windows build/container hosts, not automatic PC setup.
# The upstream x86 lua5.1.dll imports MSVCR110.dll (Visual C++ 2012).
$ErrorActionPreference = 'Stop'
$runtimeDirectory = if ([Environment]::Is64BitOperatingSystem) { 'SysWOW64' } else { 'System32' }
$runtimeLibrary = Join-Path $env:WINDIR ($runtimeDirectory + '/msvcr110.dll')
if (Test-Path -LiteralPath $runtimeLibrary) {
    Write-Output 'The x86 Visual C++ 2012 runtime is already installed.'
    return
}
$installer = Join-Path $env:TEMP 'wherigo-vcredist2012-x86.exe'
Invoke-WebRequest 'https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe' -OutFile $installer
$expectedHash = 'B924AD8062EAF4E70437C8BE50FA612162795FF0839479546CE907FFA8D6E386'
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Microsoft runtime installer hash mismatch.' }
$signature = Get-AuthenticodeSignature -LiteralPath $installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Microsoft Corporation(,|$)') { throw 'Microsoft runtime installer signature is not valid.' }
$installation = Start-Process -FilePath $installer -ArgumentList '/install','/quiet','/norestart' -WindowStyle Hidden -Wait -PassThru
if ($installation.ExitCode -notin @(0,3010,1638)) { throw ('Microsoft runtime installation failed: ' + $installation.ExitCode) }
if (!(Test-Path -LiteralPath $runtimeLibrary)) { throw 'The x86 runtime is still missing after installation.' }
Remove-Item -LiteralPath $installer
Write-Output 'Installed the official x86 Visual C++ 2012 runtime.'
