<#
.DESCRIPTION
	TinyIpc build script, run using cmdlet Invoke-Build from module InvokeBuild
#>
param (
    [string]
	$Version
)

task Test {
	exec { dotnet test .\test\TinyIpc.Tests\TinyIpc.Tests.csproj }
}

task AssertVersion {
	if (-not $Version) {
		throw "Specify version with -Version parameter"
	}
}

task Package {
	$outputPath = (Get-Item ".").FullName
	exec { dotnet pack .\src\TinyIpc\TinyIpc.csproj --configuration Release --output $outputPath /p:Version=$Version /p:EnableSourcelink="true" }
}

task . AssertVersion, Test, Package
