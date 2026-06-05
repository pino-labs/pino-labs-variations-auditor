param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "..\..\artifacts\nuget",
    # Optional NuGet package version override (e.g. CI passes the tag/build version).
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$frontendDir = Join-Path $repoRoot "frontend"
$addonProject = Join-Path $PSScriptRoot "PiNo.Labs.VariationsAuditor.Addon.csproj"
$packageOutPath = Join-Path $PSScriptRoot $OutputDir
if (-not (Test-Path $packageOutPath)) {
    New-Item -ItemType Directory -Path $packageOutPath -Force | Out-Null
}
$packageOut = Resolve-Path $packageOutPath

Write-Host "[1/4] Building frontend bundle..."
Push-Location $frontendDir
try {
    # Prefer a clean, reproducible install when a lockfile is present (CI); fall back to install.
    if (Test-Path (Join-Path $frontendDir "package-lock.json")) {
        npm ci
    } else {
        npm install
    }
    if ($LASTEXITCODE -ne 0) { throw "npm install/ci failed with exit code $LASTEXITCODE" }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}

Write-Host "[2/4] Packing NuGet package..."
$packArgs = @($addonProject, "-c", $Configuration, "-o", $packageOut, "--nologo")
if ($Version) {
    $packArgs += "-p:Version=$Version"
}
dotnet pack @packArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE" }


Write-Host "[3/4] Validating package content..."
$pkg = Get-ChildItem $packageOut -Filter "PiNo.Labs.VariationsAuditor.Addon.*.nupkg" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $pkg) {
    throw "Package file not found in $packageOut"
}

$tmpZip = "$($pkg.FullName).zip"
$tmpDir = Join-Path $repoRoot ".tmp_va_pack_verify"
if (Test-Path $tmpZip) { Remove-Item $tmpZip -Force }
if (Test-Path $tmpDir) { Remove-Item $tmpDir -Recurse -Force }
Copy-Item $pkg.FullName $tmpZip
Expand-Archive -Path $tmpZip -DestinationPath $tmpDir

$mustExist = @(
    "buildTransitive/PiNo.Labs.VariationsAuditor.Addon.targets",
    "contentFiles/any/any/modules/_protected/PiNo.Labs.VariationsAuditor/module.config",
    "lib/net10.0/PiNo.Labs.VariationsAuditor.Addon.dll",
    # The feature code + embedded UI live in the Core assembly, which the addon bundles into lib/.
    "lib/net10.0/PiNo.Labs.VariationsAuditor.Core.dll"
)

foreach ($relative in $mustExist) {
    $full = Join-Path $tmpDir ($relative -replace '/', '\\')
    if (-not (Test-Path $full)) {
        throw "Missing required package entry: $relative"
    }
}

Write-Host "[3b/4] Verifying the React bundle is embedded in the Core assembly..."
# The bundle is embedded in PiNo.Labs.VariationsAuditor.Core.dll (the single shared assembly that
# VariationsAuditorAssets resolves embedded resources from), NOT the thin addon wrapper DLL.
$dllPath = Join-Path $tmpDir "lib\net10.0\PiNo.Labs.VariationsAuditor.Core.dll"
# Scan the raw PE/metadata bytes for the embedded resource names. This is runtime-independent
# (works under Windows PowerShell 5.1) and avoids loading a net10 assembly into the host.
$asciiText = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($dllPath))
if ($asciiText -notmatch 'EmbeddedFrontend\.index\.html') {
    throw "Embedded bundle missing: 'EmbeddedFrontend.index.html' resource not found in the assembly."
}
if ($asciiText -notmatch 'EmbeddedFrontend\.assets\.') {
    throw "Embedded bundle missing: no 'EmbeddedFrontend.assets.*' resources found in the assembly."
}
Write-Host "      OK - embedded UI bundle (index.html + assets) found in the assembly"

Write-Host "[4/4] Done"
Write-Host "Package: $($pkg.FullName)"


