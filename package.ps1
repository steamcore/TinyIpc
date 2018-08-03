param (
    [Parameter(Mandatory=$True)]
    [string]
    $Version
)

function Assert-SuccessfulExitCode {
    if ($LastExitCode -ne 0) {
        Write-Error "Build failed with code $LastExitCode"
        throw ("Build failed with code " + $LastExitCode)
    }
}

function Enter-Task($message, $action) {
    Write-Host $message -ForegroundColor Yellow
    & $action
    Assert-SuccessfulExitCode
}

Enter-Task "Test" {
	dotnet test .\test\TinyIpc.Tests\TinyIpc.Tests.csproj
}

Enter-Task "Package" {
	$outputPath = (Get-Item ".").FullName
	dotnet pack .\src\TinyIpc\TinyIpc.csproj --configuration Release --output $outputPath /p:Version=$Version
}
