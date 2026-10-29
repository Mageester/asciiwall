<#
  Builds AsciiWall with nothing but what ships in Windows: the .NET Framework C# compiler.

    powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1

  Output (in desktop\dist):
    AsciiWall-Setup.exe     the installer (also the uninstaller once installed)
    AsciiWall\              the same app as a portable folder: run AsciiWall\AsciiWall.exe
#>
param([switch]$RedrawIcon)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'AsciiWall'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Could not find the C# compiler at $csc (the .NET Framework 4 ships with Windows 10 and 11)." }

$ico = Join-Path $root 'assets\AsciiWall.ico'
if ($RedrawIcon -or -not (Test-Path $ico)) {
    & powershell.exe -NoProfile -File (Join-Path $root 'tools\make-icon.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Drawing the icon failed.' }
}

if (Test-Path $dist) { [System.IO.Directory]::Delete($dist, $true) }
New-Item -ItemType Directory -Force $stage | Out-Null

$refs = 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll'
$host_ = Join-Path $root 'host'
$sources = 'Native.cs', 'Config.cs', 'Ipc.cs', 'Wallpaper.cs', 'Server.cs', 'Host.cs', 'Tray.cs', 'Program.cs' | ForEach-Object { Join-Path $host_ $_ }
$args1 = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warn:4', '/warnaserror+',
    "/out:$stage\AsciiWall.exe", "/win32icon:$ico", "/win32manifest:$host_\app.manifest") + ($refs | ForEach-Object { "/r:$_" }) + $sources
Write-Host 'Compiling AsciiWall.exe...'
& $csc @args1
if ($LASTEXITCODE -ne 0) { throw 'Compiling AsciiWall.exe failed.' }

Write-Host 'Compiling AsciiWallCtl.exe...'
& $csc /nologo /target:exe /platform:x64 /optimize+ /warn:4 /warnaserror+ "/out:$stage\AsciiWallCtl.exe" "/win32icon:$ico" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$host_\Ipc.cs" "$host_\Cli.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compiling AsciiWallCtl.exe failed.' }

Write-Host 'Copying the web pages...'
Copy-Item (Join-Path $root 'web') (Join-Path $stage 'web') -Recurse
foreach ($f in 'LICENSE', 'README.md') {
    $p = Join-Path $root $f
    if (Test-Path $p) { Copy-Item $p $stage }
}

Write-Host 'Packing the installer...'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $dist 'payload.zip'
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$refs2 = 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll'
$args2 = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warn:4', '/warnaserror+',
    "/out:$dist\AsciiWall-Setup.exe", "/win32icon:$ico", "/win32manifest:$host_\app.manifest", "/resource:$zip,payload.zip") +
    ($refs2 | ForEach-Object { "/r:$_" }) + (Join-Path $root 'installer\Setup.cs')
& $csc @args2
if ($LASTEXITCODE -ne 0) { throw 'Compiling the installer failed.' }
Remove-Item $zip

Get-ChildItem $dist -File | ForEach-Object { '{0,-24} {1,8:N0} KB' -f $_.Name, ($_.Length / 1KB) }
Write-Host "`nDone. Run $dist\AsciiWall-Setup.exe to install."
