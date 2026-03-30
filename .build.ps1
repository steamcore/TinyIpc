<#
.DESCRIPTION
	TinyIpc build script, run using cmdlet Invoke-Build from module InvokeBuild
#>
param (
    [string]
    $Version
)

$artifactFolder = "artifacts"
$testResultsFolder = Join-Path $artifactFolder "testresults"

task AssertVersion {
    if (-not $Version) {
        throw "Specify version with -Version parameter"
    }
}

task DotnetRestore {
    exec {
        dotnet restore
    }
}

task DotnetFormat DotnetRestore, {
    exec {
        dotnet format --no-restore
    }
}

task DotnetFormatCheck DotnetRestore, {
    exec {
        dotnet format --no-restore --verify-no-changes
    }
}

task DotnetBuild DotnetRestore, {
    exec {
        dotnet build --no-restore
    }
}

task DotnetTest DotnetBuild, {
    exec {
        dotnet test `
            --disable-logo `
            --no-build `
            --max-parallel-test-modules 1 `
            --results-directory $testResultsFolder
    }
}

task AotTest {
    $binary = Join-Path "artifacts" "publish" "TinyIpc.Tests" "release_net10.0-windows_win-x64" "TinyIpc.Tests.exe"

    exec {
        dotnet publish (Join-Path "test" "TinyIpc.Tests") `
            --framework "net10.0-windows" `
            -p:"Aot=true"
    }

    exec {
        & $binary --disable-logo --results-directory $testResultsFolder
    }
}

task DotnetPack AssertVersion, {
    exec {
        dotnet pack (Join-Path "src" "TinyIpc") `
            --configuration Release `
            --output . `
            /p:ContinuousIntegrationBuild="true" `
            /p:EnableSourcelink="true" `
            /p:Version=$Version
    }
}

task Package DotnetPack

task . DotnetFormatCheck, DotnetBuild, DotnetTest
