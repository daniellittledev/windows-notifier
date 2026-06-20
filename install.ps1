<#
.SYNOPSIS
  Download and install the Windows CLI Notifier (notifier.exe, register.exe, viewmd.exe).

.DESCRIPTION
  Fetches a release zip from GitHub, extracts the executables to a stable per-user
  folder, and adds that folder to your user PATH. Everything is written under your
  user profile -- no administrator rights required.

  Registration is left as an explicit step: after installing, run register.exe once
  yourself (it is required -- without it toasts silently never appear). Pass -Register
  to have this script run it for you as part of install.

.PARAMETER Version
  Release tag to install, e.g. 'v1.0.0'. Defaults to the latest release.

.PARAMETER InstallDir
  Where to install. Defaults to "$env:LOCALAPPDATA\Programs\notifier".

.PARAMETER Register
  Also run register.exe against the installed notifier.exe. Off by default so that
  registration stays an explicit, separate step.

.PARAMETER NoPath
  Skip adding InstallDir to your user PATH.

.EXAMPLE
  irm https://raw.githubusercontent.com/daniellittledev/windows-notifier/main/install.ps1 | iex

  Installs the latest release to %LOCALAPPDATA%\Programs\notifier and adds it to PATH.

.EXAMPLE
  & ([scriptblock]::Create((irm https://raw.githubusercontent.com/daniellittledev/windows-notifier/main/install.ps1))) -Version v1.0.0 -Register

  Installs a specific version and registers it as part of the install.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\notifier'),
    [switch]$Register,
    [switch]$NoPath
)

$ErrorActionPreference = 'Stop'

# This tool is Windows-only (WinRT toasts). Guard PowerShell 6+ on other platforms.
if ($PSVersionTable.PSVersion.Major -ge 6 -and -not $IsWindows) {
    throw 'The Windows CLI Notifier only runs on Windows.'
}

# Windows PowerShell 5.1 defaults to TLS 1.0; GitHub requires TLS 1.2+.
[Net.ServicePointManager]::SecurityProtocol =
    [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$repo = 'daniellittledev/windows-notifier'

function Get-Release {
    param([string]$Version)
    $api =
        if ($Version) { "https://api.github.com/repos/$repo/releases/tags/$Version" }
        else          { "https://api.github.com/repos/$repo/releases/latest" }
    $headers = @{ 'User-Agent' = 'windows-notifier-install'; 'Accept' = 'application/vnd.github+json' }
    try {
        Invoke-RestMethod -Uri $api -Headers $headers -UseBasicParsing
    } catch {
        throw "Could not fetch release info from $api`n  $($_.Exception.Message)"
    }
}

Write-Host 'Resolving release...' -ForegroundColor Cyan
$release = Get-Release -Version $Version
$tag = $release.tag_name
$asset = $release.assets | Where-Object { $_.name -like '*-win-x64.zip' } | Select-Object -First 1
if (-not $asset) {
    throw "Release '$tag' has no *-win-x64.zip asset."
}
$sumsAsset = $release.assets | Where-Object { $_.name -eq 'SHA256SUMS' } | Select-Object -First 1
if (-not $sumsAsset) {
    throw "Release '$tag' has no SHA256SUMS asset to verify against. (Releases cut before checksum publishing was added are not installable with this script -- install a newer release, or download by hand.)"
}

# Defence-in-depth: only ever fetch over HTTPS, even if the API hands us something else.
foreach ($a in @($asset, $sumsAsset)) {
    if ($a.browser_download_url -notlike 'https://*') {
        throw "Refusing to download '$($a.name)' over a non-HTTPS URL: $($a.browser_download_url)"
    }
}
Write-Host "  Version: $tag"
Write-Host "  Asset:   $($asset.name)"

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('notifier-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
$zip = Join-Path $tmp $asset.name
$sums = Join-Path $tmp 'SHA256SUMS'
try {
    Write-Host 'Downloading...' -ForegroundColor Cyan
    Invoke-WebRequest -Uri $asset.browser_download_url     -OutFile $zip  -UseBasicParsing
    Invoke-WebRequest -Uri $sumsAsset.browser_download_url -OutFile $sums -UseBasicParsing

    Write-Host 'Verifying SHA-256 checksum...' -ForegroundColor Cyan
    $expected = $null
    foreach ($line in Get-Content $sums) {
        $f = $line -split '\s+', 2
        if ($f.Count -eq 2 -and $f[1].Trim().TrimStart('*') -eq $asset.name) { $expected = $f[0].Trim(); break }
    }
    if (-not $expected) {
        throw "SHA256SUMS has no entry for '$($asset.name)' -- refusing to install unverified."
    }
    $actual = (Get-FileHash -Algorithm SHA256 -Path $zip).Hash
    if ($actual.ToLower() -ne $expected.ToLower()) {
        throw "Checksum mismatch for '$($asset.name)'.`n  expected: $expected`n  actual:   $actual`nThe download may be corrupt or tampered with -- not installing."
    }
    Write-Host '  OK' -ForegroundColor Green

    Write-Host "Extracting to $InstallDir" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Expand-Archive -Path $zip -DestinationPath $InstallDir -Force
} finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

$notifier = Join-Path $InstallDir 'notifier.exe'
if (-not (Test-Path $notifier)) {
    throw "Expected notifier.exe at '$notifier' after extraction, but it is missing."
}

# Add to the user PATH (idempotent), and to this session so register/verify work now.
# Read/write the RAW registry value as REG_EXPAND_SZ so existing %VAR% entries in the
# user's PATH stay dynamic instead of being expanded to static literals. (A direct
# registry write doesn't broadcast WM_SETTINGCHANGE, so already-open apps pick it up
# only after restart -- hence the "open a new terminal" note below.)
if (-not $NoPath) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    try {
        $raw = $key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $parts = @()
        if ($raw) { $parts = $raw -split ';' | Where-Object { $_ -ne '' } }
        if ($parts -notcontains $InstallDir) {
            $key.SetValue('Path', (@($parts + $InstallDir) -join ';'), [Microsoft.Win32.RegistryValueKind]::ExpandString)
            Write-Host "Added to user PATH: $InstallDir" -ForegroundColor Green
        } else {
            Write-Host "Already on user PATH: $InstallDir"
        }
    } finally {
        if ($key) { $key.Dispose() }
    }
    if (($env:Path -split ';') -notcontains $InstallDir) { $env:Path += ";$InstallDir" }
}

Write-Host ''
Write-Host 'Installed:' -ForegroundColor Green
Get-ChildItem $InstallDir -Filter *.exe | ForEach-Object { Write-Host "  $($_.FullName)" }

if ($Register) {
    Write-Host ''
    Write-Host "Registering (--target $notifier)..." -ForegroundColor Cyan
    & (Join-Path $InstallDir 'register.exe') --target $notifier
    Write-Host ''
    Write-Host 'Done. Verify with:' -ForegroundColor Green
    Write-Host "  notifier --title 'Hello' --message 'it works'"
} else {
    Write-Host ''
    Write-Host 'Next step -- register once (required, no admin needed):' -ForegroundColor Yellow
    Write-Host "  register.exe"
    Write-Host ''
    Write-Host 'Then raise a toast:'
    Write-Host "  notifier --title 'Hello' --message 'it works'"
    Write-Host ''
    Write-Host 'Open a NEW terminal first so the updated PATH is picked up.'
}
