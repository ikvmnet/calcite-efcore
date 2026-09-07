# Reports which snapshot build each project will actually compile against.
#
# A MavenReference on "1.43.0-SNAPSHOT" does not name a build. The resolver reads the remote
# maven-metadata.xml to learn the latest timestamped build, but the file IKVM hands to the
# compiler is the non-timestamped alias in the local repository -- and that alias is replaced
# only when a project re-resolves. Between those two moments the alias can hold an older build
# than the metadata beside it advertises, and nothing in the build output says so.
#
# This script compares, per artifact, the build the remote advertises against the build the
# alias actually holds, and lists the projects whose obj cache pins the resolution.
#
#   pwsh tools\check-snapshot.ps1
#   pwsh tools\check-snapshot.ps1 -Refresh    # delete the obj caches of stale projects
[CmdletBinding()]
param(
    # Repository root; defaults to the parent of this script's directory.
    [string] $Root = (Split-Path -Parent $PSScriptRoot),
    # Snapshot repository the artifacts come from.
    [string] $Repository = 'https://repository.apache.org/content/repositories/snapshots',
    # Local Maven repository holding the alias jars.
    [string] $LocalRepository = (Join-Path $env:USERPROFILE '.m2\repository'),
    # Delete the obj Maven caches of any project pinned to a stale alias, forcing a re-resolve.
    [switch] $Refresh
)

$ErrorActionPreference = 'Stop'

# --- every MavenReference in the tree, with the project that declares it -------------------

$references = @()
foreach ($project in Get-ChildItem -Path (Join-Path $Root 'src') -Filter *.csproj -Recurse -File) {
    if ($project.FullName -like '*\obj\*' -or $project.FullName -like '*\bin\*') { continue }
    foreach ($match in [regex]::Matches(
        (Get-Content -Raw -LiteralPath $project.FullName),
        '<MavenReference\s+Include="(?<id>[^"]+)"\s+Version="(?<version>[^"]+)"')) {
        $references += [pscustomobject]@{
            Project = $project
            Id      = $match.Groups['id'].Value
            Version = $match.Groups['version'].Value
        }
    }
}

$snapshots = $references | Where-Object { $_.Version -like '*-SNAPSHOT' }
if (-not $snapshots) {
    Write-Host 'No snapshot MavenReferences in the tree.'
    return
}

# --- what each artifact's alias actually holds ---------------------------------------------

function Get-RemoteText([string] $url) {
    try {
        # Nexus serves .sha1 as octet-stream, which comes back as a byte array rather than text.
        $content = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30).Content
        if ($content -is [byte[]]) { $content = [System.Text.Encoding]::UTF8.GetString($content) }
        [string]$content.Trim()
    }
    catch { $null }
}

$stale = @{}
$rows = @()

foreach ($group in $snapshots | Group-Object { "$($_.Id):$($_.Version)" } | Sort-Object Name) {
    $group.Group[0].Id -match '^(?<group>[^:]+):(?<artifact>.+)$' | Out-Null
    $groupId = $Matches['group']
    $artifactId = $Matches['artifact']
    $version = $group.Group[0].Version

    $path = "$($groupId -replace '\.','/')/$artifactId/$version"
    $metadata = Get-RemoteText "$Repository/$path/maven-metadata.xml"
    if (-not $metadata) {
        $rows += [pscustomobject]@{ Artifact = $artifactId; Remote = '(unreachable)'; Local = ''; State = 'unknown' }
        continue
    }

    $snapshotVersion = ([xml]$metadata).metadata.versioning.snapshotVersions.snapshotVersion |
        Where-Object { $_.extension -eq 'jar' -and -not $_.classifier } |
        Select-Object -First 1 -ExpandProperty value

    $aliasPath = Join-Path $LocalRepository "$($groupId -replace '\.','\')\$artifactId\$version\$artifactId-$version.jar"
    if (-not (Test-Path -LiteralPath $aliasPath)) {
        $rows += [pscustomobject]@{ Artifact = $artifactId; Remote = $snapshotVersion; Local = '(absent)'; State = 'absent' }
        continue
    }

    # The remote publishes a .sha1 beside each timestamped jar, so the alias can be identified
    # without downloading the jar itself.
    $remoteSha = Get-RemoteText "$Repository/$path/$artifactId-$snapshotVersion.jar.sha1"
    $localSha = (Get-FileHash -LiteralPath $aliasPath -Algorithm SHA1).Hash.ToLowerInvariant()
    if ($remoteSha) { $remoteSha = ($remoteSha -split '\s+')[0].ToLowerInvariant() }

    $state = if (-not $remoteSha) { 'unknown' } elseif ($remoteSha -eq $localSha) { 'current' } else { 'STALE' }
    if ($state -eq 'STALE') { foreach ($reference in $group.Group) { $stale[$reference.Project.FullName] = $reference.Project } }

    $rows += [pscustomobject]@{
        Artifact = $artifactId
        Remote   = $snapshotVersion
        Local    = if ($state -eq 'current') { $snapshotVersion } else { "sha1 $($localSha.Substring(0, 12))" }
        State    = $state
    }
}

$rows | Format-Table -AutoSize | Out-String | Write-Host

# --- the obj caches that pin resolution ------------------------------------------------------

Write-Host 'Projects referencing snapshots, and whether an obj cache pins their resolution:'
foreach ($group in $snapshots | Group-Object { $_.Project.FullName } | Sort-Object Name) {
    $project = $group.Group[0].Project
    $caches = @(Get-ChildItem -Path (Join-Path $project.DirectoryName 'obj') -Filter *.maven.cache -Recurse -File -ErrorAction SilentlyContinue)
    $note = if ($caches) { "pinned by $($caches.Count) cache file(s)" } else { 'no cache; will resolve' }
    Write-Host ("  {0,-58} {1}" -f $project.BaseName, $note)
}

$unknown = @($rows | Where-Object State -eq 'unknown')
if ($unknown) {
    Write-Host "`n$($unknown.Count) artifact(s) could not be checked against the remote; nothing is claimed about them."
}

if ($stale.Count -eq 0) {
    if (-not $unknown) { Write-Host "`nEvery alias matches the build the remote advertises." }
    return
}

Write-Host "`n$($stale.Count) project(s) would compile against a stale alias:"
foreach ($project in $stale.Values) { Write-Host "  $($project.BaseName)" }

if (-not $Refresh) {
    Write-Host "`nRe-run with -Refresh to delete their obj Maven caches and force a re-resolve."
    return
}

foreach ($project in $stale.Values) {
    foreach ($cache in Get-ChildItem -Path (Join-Path $project.DirectoryName 'obj') -Filter *.maven.cache -Recurse -File -ErrorAction SilentlyContinue) {
        Remove-Item -LiteralPath $cache.FullName -Force
        Write-Host "  removed $($cache.FullName)"
    }
}
Write-Host "`nBuild those projects to re-resolve. Build single-threaded (-m:1) -- concurrent"
Write-Host 'resolutions race to install the same alias, and the losers leave .tmp files behind.'
