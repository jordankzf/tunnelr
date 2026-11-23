# Build a single executable and install it at the same path on every upgrade.
$ErrorActionPreference = 'Stop'
$build = Join-Path $PSScriptRoot 'out\v2'
dotnet publish (Join-Path $PSScriptRoot 'tunnelr.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $build
if ($LASTEXITCODE -ne 0) { throw 'Tunnelr publish failed.' }

$install = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Tunnelr'
[IO.Directory]::CreateDirectory($install) | Out-Null
$target = Join-Path $install 'tunnelr.exe'
$staged = Join-Path $install ([IO.Path]::GetRandomFileName())
try {
    [IO.File]::Copy((Join-Path $build 'tunnelr.exe'), $staged)
    if ([IO.File]::Exists($target)) {
        [IO.File]::Replace($staged, $target, ($target + '.previous'))
    } else {
        [IO.File]::Move($staged, $target)
    }
} finally {
    if ([IO.File]::Exists($staged)) { [IO.File]::Delete($staged) }
}
$startup = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemPropertyValue $startup -Name Tunnelr -ErrorAction SilentlyContinue) {
    Set-ItemProperty $startup -Name Tunnelr -Value ('"' + $target + '"')
}
Write-Output "Installed $target"
