#Requires -Version 7.0
param(
    [string]$Tag = "goverp-web:local",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$publishDir = Join-Path $Root "artifacts/publish/GovErp.Web"
Write-Host "Building solution ($Configuration)..."
dotnet restore GovErp.sln
dotnet build GovErp.sln -c $Configuration --no-restore

Write-Host "Publishing GovErp.Web..."
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
dotnet publish src/GovErp.Web/GovErp.Web.csproj -c $Configuration -o $publishDir

Write-Host "Building Docker image $Tag..."
docker build -f src/GovErp.Web/Dockerfile -t $Tag $Root
Write-Host "Done. Push with: docker push $Tag"
Write-Host "Or set GOVERP_WEB_IMAGE=$Tag in docker/.env and: docker compose -f docker/docker-compose.yml up"
