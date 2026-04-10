Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
$buildDirectory = Join-Path $root 'build'
$buildTempDirectory = Join-Path $root 'build_tmp'
$launcherPublish = Join-Path $buildTempDirectory 'launcher_publish'
$mainPublish = Join-Path $buildTempDirectory 'main_publish'
$launcherBuildDirectory = Join-Path $buildDirectory 'launcher'

function Remove-DirectoryIfExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcePath,

        [Parameter(Mandatory = $true)]
        [string] $DestinationDirectory
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Required publish output not found: $SourcePath"
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationDirectory -Force
}

function Copy-MainPayload {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string] $DestinationDirectory
    )

    Get-ChildItem -LiteralPath $SourceDirectory -File | Where-Object {
        $_.Extension -notin '.pdb', '.log', '.tmp', '.bak'
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $DestinationDirectory -Force
    }

    Get-ChildItem -LiteralPath $SourceDirectory -Directory | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $DestinationDirectory -Recurse -Force
    }
}

Remove-DirectoryIfExists -Path $buildDirectory
Remove-DirectoryIfExists -Path $buildTempDirectory

New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $buildTempDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $launcherPublish -Force | Out-Null
New-Item -ItemType Directory -Path $mainPublish -Force | Out-Null
New-Item -ItemType Directory -Path $launcherBuildDirectory -Force | Out-Null

try {
    dotnet publish (Join-Path $root 'LazyBootstrap.Launcher/LazyBootstrap.Launcher.csproj') -c Release -r win-x64 -o $launcherPublish
    dotnet publish (Join-Path $root 'LazyBootstrap/LazyBootstrap.csproj') -c Release -r win-x64 --self-contained true -p:PublishAot=false -p:PublishTrimmed=false -o $mainPublish

    Get-ChildItem -Path $mainPublish -Recurse -File | Where-Object {
        $_.Name -eq 'LazyBootstrap.log' -or
        $_.Extension -in '.log', '.tmp', '.bak'
    } | Remove-Item -Force

    Copy-RequiredFile -SourcePath (Join-Path $launcherPublish 'LazyBootstrap.exe') -DestinationDirectory $buildDirectory
    Copy-MainPayload -SourceDirectory $mainPublish -DestinationDirectory $launcherBuildDirectory

    Write-Host "Build completed: $buildDirectory"
}
finally {
    Remove-DirectoryIfExists -Path $buildTempDirectory
}
