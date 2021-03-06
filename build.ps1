param (
    [Parameter(Mandatory=$true)][int]$BuildNumber,
    [Parameter(Mandatory=$false)][string]$CommitSHA
)

$pwd = Pwd
$basePath = Join-Path $pwd SimpleRedirect
$csProjPath = Join-Path $basePath SimpleRedirect.csproj
$buildPath = Join-Path $basePath build

cp $csProjPath "$($csProjPath).original"

[xml]$xmlDoc = Get-Content $csProjPath
$versionElement = $xmlDoc['Project']['PropertyGroup']['Version']
$version = [version]$versionElement.InnerText
$newVersion = "$($version.Major).$($version.Minor).$($BuildNumber)"

$versionWithoutHash = $newVersion
if ($CommitSHA) {
    $newVersion = "$($newVersion)+$($CommitSHA.SubString(0, 7))"
}

$versionElement.InnerText = $newVersion
$xmlDoc.Save($csProjPath)

if (Test-Path $buildPath -PathType Container) {
    rm -rf $buildPath
}

$uid = sh -c 'id -u'
$gid = sh -c 'id -g'

docker run --rm -v "$($basePath):/var/src" mcr.microsoft.com/dotnet/core/sdk:3.1.404-alpine3.12 ash -c "dotnet publish -c Release /var/src/SimpleRedirect.csproj -o /var/src/build && chown -R $($uid):$($gid) /var/src"

mv "$($csProjPath).original" $csProjPath

$nuspecPath = Join-Path $buildPath simpleredirect.nuspec
$nupkgPath = Join-Path $buildPath "simpleredirect.$($newVersion).nupkg"
cp simpleredirect.nuspec $nuspecPath

[xml]$xmlDoc = Get-Content $nuspecPath
$xmlDoc['package']['metadata']['version'].InnerText = $versionWithoutHash
$xmlDoc.Save($nuspecPath)

Compress-Archive -Path "$($buildPath)/*" -DestinationPath $nupkgPath

if ($env:GITHUB_ENV) {
    Write-Output "VERSION_WITHOUT_HASH=$versionWithoutHash" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
    Write-Output "VERSION=$newVersion" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
    Write-Output "PKG_PATH=$nupkgPath" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
}
