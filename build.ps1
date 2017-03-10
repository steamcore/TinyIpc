if (!(Test-Path "build")) {
	mkdir "build"
}

$outputPath = (Get-Item "build").FullName

dotnet restore
dotnet test .\test\TinyIpc.Tests\TinyIpc.Tests.csproj

if ($LASTEXITCODE -ne 0) {
	throw "Tests failed"
}

dotnet pack .\src\TinyIpc\TinyIpc.csproj --configuration Release --output $outputPath
