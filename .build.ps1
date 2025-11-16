<#
.DESCRIPTION
	TinyIpc build script, run using cmdlet Invoke-Build from module InvokeBuild
#>
param (
    [string]
    $Version
)

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
    Push-Location "./test/TinyIpc.Tests"

    exec {
        dotnet test --disable-logo --no-build --max-parallel-test-modules 1
    }
}

task AotTest {
    Push-Location "./test/TinyIpc.Tests"
    exec {
        dotnet publish /p:"Aot=true" --framework "net10.0-windows"
    }
    Pop-Location

    Push-Location "./artifacts/publish/TinyIpc.Tests/release_net10.0-windows_win-x64/"
    exec {
        .\TinyIpc.Tests.exe --disable-logo
    }
}

task DotnetPack AssertVersion, {
    exec {
        dotnet pack ".\src\TinyIpc\TinyIpc.csproj" `
            --configuration Release `
            --output . `
            /p:ContinuousIntegrationBuild="true" `
            /p:EnableSourcelink="true" `
            /p:Version=$Version
    }
}

task Package DotnetPack

task . DotnetFormatCheck, DotnetBuild, DotnetTest
