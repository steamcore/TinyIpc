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

task DotnetToolRestore {
    exec {
        dotnet tool restore
    }
}

task DotnetRestore {
    exec {
        dotnet restore
    }
}

task DotnetFormat DotnetToolRestore, DotnetRestore, {
    exec {
        dotnet format --no-restore
    }
}

task DotnetFormatCheck DotnetToolRestore, DotnetRestore, {
    exec {
        dotnet format --no-restore --verify-no-changes
    }
}

task DotnetBuild DotnetToolRestore, DotnetRestore, {
    exec {
        dotnet build --no-restore
    }
}

task DotnetTest DotnetBuild, {
    exec {
        dotnet test .\test\TinyIpc.Tests\TinyIpc.Tests.csproj
    }
}

task DotnetPack AssertVersion, {
    exec {
        dotnet pack .\src\TinyIpc\TinyIpc.csproj `
            --configuration Release `
            --output . `
            /p:ContinuousIntegrationBuild="true" `
            /p:EnableSourcelink="true" `
            /p:Version=$Version
    }
}

task Package DotnetPack

task . DotnetFormatCheck, DotnetBuild, DotnetTest
