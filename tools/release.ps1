# Cuts a release: bumps the version in MacShortcuts.csproj, rolls CHANGELOG.md, commits,
# tags vX.Y.Z and pushes. The pushed tag starts .github/workflows/release.yml, which builds
# the exe and publishes the GitHub release.
#
# Run it with no arguments and it asks for everything. Arguments skip the matching question:
#   tools/release.ps1                       interactive
#   tools/release.ps1 -Bump minor           skip the bump question
#   tools/release.ps1 -Bump patch -DryRun   show what would happen, change nothing
#   tools/release.ps1 -Bump patch -Execute  skip the final dry-run/go-ahead question
param(
    [ValidateSet('major', 'minor', 'patch')][string]$Bump,
    [switch]$DryRun,
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$csproj = Join-Path $root 'src/MacShortcuts/MacShortcuts.csproj'
$changelogPath = Join-Path $root 'CHANGELOG.md'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Invoke-Git {
    $out = & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed" }
    $out
}

function Ask([string]$question, [string[]]$options, [int]$default = 0) {
    Write-Host ''
    Write-Host $question
    for ($i = 0; $i -lt $options.Count; $i++) {
        $mark = if ($i -eq $default) { ' (default)' } else { '' }
        Write-Host ("  {0}) {1}{2}" -f ($i + 1), $options[$i], $mark)
    }
    while ($true) {
        $answer = Read-Host 'Choose'
        if ($answer -eq '') { return $default }
        $n = 0
        if ([int]::TryParse($answer, [ref]$n) -and $n -ge 1 -and $n -le $options.Count) { return $n - 1 }
        Write-Host "Enter a number from 1 to $($options.Count)."
    }
}

# --- Preconditions ---------------------------------------------------------------------------
if ((Invoke-Git branch --show-current) -ne 'main') { throw 'Releases are cut from main. Switch to main first.' }
if (Invoke-Git status --porcelain) { throw 'The working tree has uncommitted changes. Commit or stash them first.' }
Invoke-Git fetch origin main --tags --quiet
if ((Invoke-Git rev-parse HEAD) -ne (Invoke-Git rev-parse origin/main)) {
    throw 'main is not in sync with origin/main. Pull or push first.'
}

# --- Version ---------------------------------------------------------------------------------
$csprojText = [IO.File]::ReadAllText($csproj)
if ($csprojText -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') { throw "No <Version>x.y.z</Version> found in $csproj" }
$current = [int[]]@($Matches[1], $Matches[2], $Matches[3])
$candidates = [ordered]@{
    patch = '{0}.{1}.{2}' -f $current[0], $current[1], ($current[2] + 1)
    minor = '{0}.{1}.0' -f $current[0], ($current[1] + 1)
    major = '{0}.0.0' -f ($current[0] + 1)
}
$currentVersion = $current -join '.'

if (-not $Bump) {
    $labels = @(
        "patch -> $($candidates.patch)  (bug fixes)",
        "minor -> $($candidates.minor)  (new features)",
        "major -> $($candidates.major)  (breaking changes)"
    )
    $Bump = @('patch', 'minor', 'major')[(Ask "Current version is $currentVersion. What kind of release is this?" $labels 1)]
}
$newVersion = $candidates[$Bump]
$tag = "v$newVersion"
if (Invoke-Git tag --list $tag) { throw "Tag $tag already exists." }

# --- Release notes ---------------------------------------------------------------------------
$changelog = if (Test-Path $changelogPath) { [IO.File]::ReadAllText($changelogPath) -replace "`r`n", "`n" }
else { "# Changelog`n`nAll notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).`n`n## [Unreleased]`n" }

if ($changelog -notmatch '(?m)^## \[Unreleased\][ \t]*$') { throw 'CHANGELOG.md has no "## [Unreleased]" heading.' }
$unreleasedPattern = '(?ms)^## \[Unreleased\][ \t]*\n(.*?)(?=^## \[|\z)'
$null = $changelog -match $unreleasedPattern
$notes = $Matches[1].Trim()

if (-not $notes) {
    $lastTag = Invoke-Git describe --tags --abbrev=0 --match 'v*' 2>$null
    $range = if ($lastTag) { "$lastTag..HEAD" } else { 'HEAD' }
    $subjects = @(Invoke-Git log $range --no-merges --format=%s | Where-Object { $_ -and $_ -notmatch '^Release v\d' })
    $notes = (($subjects | ForEach-Object { "- $_" }) -join "`n")
    Write-Host ''
    Write-Host "Release notes pre-filled from $($subjects.Count) commit(s) since $(if ($lastTag) { $lastTag } else { 'the start' })."
}
else {
    Write-Host ''
    Write-Host 'Using the notes already written under [Unreleased] in CHANGELOG.md.'
}

if ((Ask 'Edit the release notes before continuing?' @('Yes, open them in an editor', 'No, they are fine')) -eq 0) {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) "release-notes-$newVersion.md"
    [IO.File]::WriteAllText($tmp, "$notes`n", $utf8)
    $editor = if ($env:EDITOR) { $env:EDITOR } else { 'notepad' }
    # Don't wait on the editor process: Windows 11 Notepad hands the file to its running
    # instance and exits at once, so wait for the user instead.
    Start-Process $editor -ArgumentList "`"$tmp`""
    [void](Read-Host "Opened in $editor. Save the file, then press Enter here to continue")
    $notes = ([IO.File]::ReadAllText($tmp) -replace "`r`n", "`n").Trim()
    Remove-Item $tmp -ErrorAction SilentlyContinue
}
if (-not $notes) { throw 'The release notes are empty.' }

# --- Summary and final confirmation ----------------------------------------------------------
Write-Host ''
Write-Host "=== Release $tag ($currentVersion -> $newVersion, $Bump) ==="
Write-Host $notes
Write-Host '=============================================='

if ($DryRun) { $real = $false }
elseif ($Execute) { $real = $true }
else {
    $choice = Ask 'What now?' @('Dry run (change nothing, just show the steps)', 'Go ahead and release for real', 'Cancel')
    if ($choice -eq 2) { Write-Host 'Cancelled.'; return }
    $real = $choice -eq 1
}

$date = Get-Date -Format 'yyyy-MM-dd'
$steps = @(
    "Set <Version> to $newVersion in src/MacShortcuts/MacShortcuts.csproj",
    "Move the notes under a new '## [$newVersion] - $date' heading in CHANGELOG.md",
    "Commit 'Release $tag'",
    "Create annotated tag $tag",
    "Push main and $tag to origin (this starts the release build on GitHub)"
)

if (-not $real) {
    Write-Host ''
    Write-Host 'DRY RUN. Nothing was changed. A real run would:'
    $steps | ForEach-Object { Write-Host "  - $_" }
    return
}

# --- Do it -----------------------------------------------------------------------------------
$newCsproj = $csprojText -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>"
[IO.File]::WriteAllText($csproj, $newCsproj, $utf8)

$rolled = [regex]::Replace($changelog, $unreleasedPattern, "## [Unreleased]`n`n## [$newVersion] - $date`n`n$notes`n`n", 1)
[IO.File]::WriteAllText($changelogPath, $rolled.TrimEnd() + "`n", $utf8)

Invoke-Git add $csproj $changelogPath
Invoke-Git commit -m "Release $tag" --quiet
Invoke-Git tag -a $tag -m "Release $tag"
Invoke-Git push origin main
Invoke-Git push origin $tag

Write-Host ''
Write-Host "Released $tag. Watch the build: https://github.com/priomsrb/mac-shortcuts-for-windows/actions"
