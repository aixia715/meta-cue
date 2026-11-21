param([switch]$SelfContained, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'MetaCue/MetaCue.csproj'
$tests = Join-Path $PSScriptRoot 'MetaCue.Tests/MetaCue.Tests.csproj'
dotnet run --project $tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Gesture tests failed.' }
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot 'artifacts/win-x64' }
$standalone = if ($SelfContained) { 'true' } else { 'false' }
dotnet publish $project -c Release -r win-x64 --self-contained $standalone -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Write-Host "Ready: $output/MetaCue.exe"
