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

task RestoreTools {
    exec { dotnet tool restore }
}

task Restore {
    exec { dotnet restore }
}

task Format RestoreTools, Restore, {
    exec { dotnet format --fix-analyzers info --fix-style info --fix-whitespace }
}

task CheckFormat RestoreTools, Restore, {
    exec { dotnet format --check --fix-analyzers info --fix-style info --fix-whitespace }
}

task Build Restore, {
    exec { dotnet build --no-restore }
}

task Test Build, {
    exec { dotnet test .\test\TinyIpc.Tests\TinyIpc.Tests.csproj }
}

task Package AssertVersion, {
    $outputPath = (Get-Item ".").FullName
    exec {
        dotnet pack .\src\TinyIpc\TinyIpc.csproj `
            --configuration Release `
            --output $outputPath `
            /p:ContinuousIntegrationBuild="true" `
            /p:EnableSourcelink="true" `
            /p:Version=$Version
    }
}

task . CheckFormat, Build, Test
