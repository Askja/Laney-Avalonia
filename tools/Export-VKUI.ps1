param(
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "..\VKUI-Avalonia"),
    [switch]$InitGit,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$vkuiSource = Join-Path $repoRoot "VKUI"
$packagesProps = Join-Path $repoRoot "Directory.Packages.props"
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)

if (-not (Test-Path -LiteralPath $vkuiSource)) {
    throw "VKUI source folder not found: $vkuiSource"
}

if (Test-Path -LiteralPath $output) {
    if (-not $Force) {
        throw "Output path already exists: $output. Pass -Force if this is intentional."
    }

    Remove-Item -LiteralPath $output -Recurse -Force
}

New-Item -ItemType Directory -Path $output | Out-Null

$vkuiTarget = Join-Path $output "VKUI"
New-Item -ItemType Directory -Path $vkuiTarget | Out-Null

$excludedDirectories = @("bin", "obj", ".git")
Get-ChildItem -LiteralPath $vkuiSource -Force | Where-Object {
    -not ($_.PSIsContainer -and $excludedDirectories -contains $_.Name)
} | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $vkuiTarget -Recurse -Force
}

$packageIds = @(
    "Avalonia",
    "Avalonia.Controls.ItemsRepeater",
    "Avalonia.Desktop",
    "Avalonia.Themes.Simple",
    "Tmds.DBus.Protocol"
)

[xml]$packages = Get-Content -LiteralPath $packagesProps -Raw
$versions = @{}
foreach ($versionNode in $packages.Project.ItemGroup.PackageVersion) {
    $id = $versionNode.Include
    if ($packageIds -contains $id) {
        $versions[$id] = $versionNode.Version
    }
}

$missing = @($packageIds | Where-Object { -not $versions.ContainsKey($_) })
if ($missing.Count -gt 0) {
    throw "Missing package versions in Directory.Packages.props: $($missing -join ', ')"
}

$propsLines = @(
    "<Project>",
    "  <PropertyGroup>",
    "    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>",
    "  </PropertyGroup>",
    "",
    "  <ItemGroup>"
)

foreach ($id in $packageIds) {
    $propsLines += "    <PackageVersion Include=""$id"" Version=""$($versions[$id])"" />"
}

$propsLines += @(
    "  </ItemGroup>",
    "</Project>"
)

Set-Content -LiteralPath (Join-Path $output "Directory.Packages.props") -Value $propsLines -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $vkuiTarget "README.md") -Destination (Join-Path $output "README.md") -Force

Set-Content -LiteralPath (Join-Path $output ".gitignore") -Encoding UTF8 -Value @(
    "bin/",
    "obj/",
    ".vs/",
    ".idea/",
    "*.user",
    "*.rsuser",
    "*.nupkg",
    "*.snupkg",
    "TestResults/",
    "*.binlog"
)

if ($InitGit) {
    git -C $output init | Out-Null
}

Write-Host "VKUI export created: $output"
Write-Host "Build check: dotnet build `"$output\VKUI\VKUI.csproj`""
