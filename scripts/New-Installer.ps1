[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\WinecloudsStudio\WinecloudsStudio.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\WinecloudsStudio.iss'
$artifactsDirectory = Join-Path $repositoryRoot 'artifacts\installer'
$publishDirectory = Join-Path $artifactsDirectory 'publish-win-x64'
$outputDirectory = Join-Path $artifactsDirectory 'output'

if (-not (Test-Path -LiteralPath $installerScript)) {
    throw "Installer script not found: $installerScript"
}

$compilerCandidates = @(
    @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
)

if ($compilerCandidates.Count -eq 0) {
    throw 'Inno Setup was not found. Install it from https://jrsoftware.org/isinfo.php, then run this script again.'
}

$compilerPath = $compilerCandidates[0]

if (Test-Path -LiteralPath $publishDirectory) {
    $resolvedArtifactsDirectory = (Resolve-Path -LiteralPath $artifactsDirectory).Path
    $resolvedPublishDirectory = (Resolve-Path -LiteralPath $publishDirectory).Path
    $artifactsPrefix = $resolvedArtifactsDirectory + [IO.Path]::DirectorySeparatorChar

    if (-not $resolvedPublishDirectory.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the installer artifacts folder: $resolvedPublishDirectory"
    }

    Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --property:PublishSingleFile=false `
    --property:PublishReadyToRun=false `
    --property:PublishTrimmed=false `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw 'Application publish failed.'
}

[xml]$project = Get-Content -LiteralPath $projectPath
$version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) {
    $version = '1.0.0'
}

& $compilerPath "/DMyAppVersion=$version" "/DMySourceDir=$publishDirectory" "/DMyOutputDir=$outputDirectory" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw 'Installer compilation failed.'
}

$installerPath = Join-Path $outputDirectory "WinecloudsStudio-Setup-$version-win-x64.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not produced: $installerPath"
}

Write-Host "Installer: $installerPath"
