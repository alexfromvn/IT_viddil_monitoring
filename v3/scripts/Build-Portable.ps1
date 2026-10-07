#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]]$RuntimeIdentifiers = @('win-x64', 'win-arm64')
)

$ErrorActionPreference = 'Stop'
$version = '3.0.1'
# Verified against official .NET release metadata and NuGet on 2026-10-07.
$runtimeVersion = '10.0.12'
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '..'))
$projectPath = Join-Path $sourceRoot 'IT_viddil_monitoring.csproj'
$releaseRoot = Join-Path $workspaceRoot "releases\v$version"
$buildStateRoot = Join-Path $sourceRoot '.build'
$dotnetCommand = Get-Command dotnet.exe -ErrorAction Stop

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & $dotnetCommand.Source @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

$previousCliHome = $env:DOTNET_CLI_HOME
$previousNugetPackages = $env:NUGET_PACKAGES
$previousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$previousDevCertificate = $env:DOTNET_GENERATE_ASPNET_CERTIFICATE
try {
    # Keep build state beside v3 rather than requiring writes to system folders.
    if ([string]::IsNullOrWhiteSpace($env:DOTNET_CLI_HOME)) {
        $env:DOTNET_CLI_HOME = Join-Path $buildStateRoot 'dotnet-cli'
    }
    if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        $env:NUGET_PACKAGES = Join-Path $buildStateRoot 'nuget-packages'
    }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    New-Item -ItemType Directory -Path $releaseRoot, $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES -Force | Out-Null

    Push-Location -LiteralPath $sourceRoot
    try {
        $sdkVersion = (& $dotnetCommand.Source --version | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.\d+\.\d+$') {
            throw "A stable .NET 10 SDK is required. Selected SDK: $sdkVersion"
        }

        $properties = @(
            "-p:RuntimeFrameworkVersion=$runtimeVersion",
            '-p:SelfContained=true',
            '-p:PublishSingleFile=true',
            '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:EnableCompressionInSingleFile=true',
            '-p:PublishTrimmed=false',
            '-p:DebugType=none',
            '-p:DebugSymbols=false'
        )

        foreach ($rid in ($RuntimeIdentifiers | Select-Object -Unique)) {
            $architecture = $rid.Substring(4)
            $outputPath = Join-Path $releaseRoot "windows-$architecture"
            Write-Host "Building IT_viddil_monitoring $version for $rid with .NET $runtimeVersion..."
            Invoke-Dotnet -Arguments (@('restore', $projectPath, '-r', $rid, '--source', 'https://api.nuget.org/v3/index.json') + $properties)
            Invoke-Dotnet -Arguments (@('publish', $projectPath, '-c', 'Release', '-r', $rid, '--self-contained', 'true', '--no-restore', '-o', $outputPath) + $properties)

            $executables = @(Get-ChildItem -LiteralPath $outputPath -File -Filter 'IT_viddil_monitoring*.exe')
            if ($executables.Count -ne 1) {
                throw "Expected one application EXE in $outputPath; found $($executables.Count)."
            }
            $executable = $executables[0]
            Copy-Item -LiteralPath (Join-Path $sourceRoot 'DISTRIBUTION.md') -Destination (Join-Path $outputPath 'DISTRIBUTION.md') -Force
            Copy-Item -LiteralPath (Join-Path $sourceRoot 'README.md') -Destination (Join-Path $outputPath 'README.md') -Force
            Copy-Item -LiteralPath (Join-Path $sourceRoot 'assets\fonts\Inter-LICENSE.txt') -Destination (Join-Path $outputPath 'Inter-LICENSE.txt') -Force
            $noticeNames = @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'PRIVACY.md', 'CODE_SIGNING_POLICY.md')
            foreach ($noticeName in $noticeNames) {
                Copy-Item -LiteralPath (Join-Path $workspaceRoot $noticeName) -Destination (Join-Path $outputPath $noticeName) -Force
            }
            $netCorePackage = Join-Path $env:NUGET_PACKAGES "microsoft.netcore.app.runtime.$rid\$runtimeVersion"
            $desktopPackage = Join-Path $env:NUGET_PACKAGES "microsoft.windowsdesktop.app.runtime.$rid\$runtimeVersion"
            $runtimeNotices = @(
                @{ Source = (Join-Path $netCorePackage 'LICENSE.TXT'); Name = 'DotNet-LICENSE.txt' },
                @{ Source = (Join-Path $netCorePackage 'THIRD-PARTY-NOTICES.TXT'); Name = 'DotNet-THIRD-PARTY-NOTICES.txt' },
                @{ Source = (Join-Path $desktopPackage 'LICENSE'); Name = 'WPF-LICENSE.txt' }
            )
            foreach ($runtimeNotice in $runtimeNotices) {
                if (-not (Test-Path -LiteralPath $runtimeNotice.Source -PathType Leaf)) {
                    throw "Required runtime notice is missing: $($runtimeNotice.Source)"
                }
                Copy-Item -LiteralPath $runtimeNotice.Source -Destination (Join-Path $outputPath $runtimeNotice.Name) -Force
            }
            $exeHash = (Get-FileHash -LiteralPath $executable.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Set-Content -LiteralPath (Join-Path $outputPath 'SHA256SUMS.txt') -Value "$exeHash  $($executable.Name)" -Encoding UTF8

            $zipPath = Join-Path $releaseRoot "IT_viddil_monitoring_v$version`_$rid`_portable.zip"
            $archiveFiles = @(
                $executable.FullName,
                (Join-Path $outputPath 'README.md'),
                (Join-Path $outputPath 'DISTRIBUTION.md'),
                (Join-Path $outputPath 'Inter-LICENSE.txt'),
                (Join-Path $outputPath 'SHA256SUMS.txt')
            )
            $archiveFiles += $noticeNames | ForEach-Object { Join-Path $outputPath $_ }
            $archiveFiles += $runtimeNotices | ForEach-Object { Join-Path $outputPath $_.Name }
            Compress-Archive -LiteralPath $archiveFiles -DestinationPath $zipPath -CompressionLevel Optimal -Force
            Write-Host "Portable archive: $zipPath"
        }

        $releaseChecksums = @()
        foreach ($rid in @('win-x64', 'win-arm64')) {
            $architecture = $rid.Substring(4)
            $outputPath = Join-Path $releaseRoot "windows-$architecture"
            if (Test-Path -LiteralPath $outputPath) {
                foreach ($executable in @(Get-ChildItem -LiteralPath $outputPath -File -Filter 'IT_viddil_monitoring*.exe')) {
                    $hash = (Get-FileHash -LiteralPath $executable.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                    $releaseChecksums += "$hash  windows-$architecture/$($executable.Name)"
                }
            }
            $zipName = "IT_viddil_monitoring_v$version`_$rid`_portable.zip"
            $zipPath = Join-Path $releaseRoot $zipName
            if (Test-Path -LiteralPath $zipPath) {
                $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
                $releaseChecksums += "$hash  $zipName"
            }
        }
        Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Value $releaseChecksums -Encoding UTF8
        Write-Host "Release complete: $releaseRoot"
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:DOTNET_CLI_HOME = $previousCliHome
    $env:NUGET_PACKAGES = $previousNugetPackages
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousTelemetry
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = $previousDevCertificate
}

