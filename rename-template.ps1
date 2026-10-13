<#
    Renames "AppTemplate" to your new project's name — both in file contents
    and in the names of files/folders that contain "AppTemplate".

    Run this RIGHT AFTER "Use this template" on the new repo, before the
    first commit in that new project.

    Usage (from the repo root):
        powershell -ExecutionPolicy Bypass -File .\rename-template.ps1 -NewName Slicice

    The script is idempotent: running it twice just finds no more "AppTemplate"
    the second time, so nothing changes. It also drops its own entry from the
    .slnx and ends with a list of any leftover "AppTemplate" it couldn't rewrite
    (a file type it doesn't know) — fix those by hand, then delete the script.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$NewName
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$Old       = 'AppTemplate'
$OldLower  = 'apptemplate'
$NewLower  = $NewName.ToLower()
$Utf8NoBom = New-Object System.Text.UTF8Encoding $false
$ScriptName = Split-Path -Leaf $PSCommandPath

if ($NewName -notmatch '^[A-Z][A-Za-z0-9]*$') {
    Write-Warning "'$NewName' doesn't look like PascalCase (e.g. 'Slicice', 'PackageTracker'). Continuing anyway, but double-check namespaces afterwards."
}

# Build output, dependencies, tool output and IDE state: never ours to rewrite.
$excludeDirs = @(
    '.git', 'bin', 'obj', 'node_modules', '.vs', '.idea', '.angular', 'dist',
    'StrykerOutput', 'TestResults', 'playwright-report', 'test-results'
)

function Test-SkipPath([string]$path) {
    $segments = $path -split '[\\/]'
    foreach ($seg in $excludeDirs) {
        if ($segments -contains $seg) { return $true }
    }
    # Worktrees the Claude desktop app keeps under .claude/ are other checkouts of this repo.
    if ($path -match '[\\/]\.claude[\\/]worktrees[\\/]') { return $true }
    return $false
}

# The text files the template holds or a project adds early: source, build, config, docs, scripts, web and deploy.
# For a dotfile such as .gitignore PowerShell reports the whole name as the extension.
$textExtensions = @(
    '.cs', '.csproj', '.sln', '.slnx', '.props', '.targets', '.json', '.xml',
    '.md', '.yml', '.yaml', '.ps1', '.sh', '.config', '.txt',
    '.editorconfig', '.gitignore', '.gitattributes', '.dockerignore',
    '.ts', '.mjs', '.js', '.html', '.scss', '.css', '.xlf', '.webmanifest', '.example', '.Dockerfile'
)

function Test-TextFile([IO.FileInfo]$file) {
    if ($textExtensions -contains $file.Extension) { return $true }
    if ($file.Name -in @('Dockerfile', 'Caddyfile')) { return $true }
    if ($file.Directory.Name -eq '.githooks') { return $true }   # the hooks have no extension
    return $false
}

# ------------------------------------------------------------- file contents
Write-Host "`n==> Replacing file contents ($Old -> $NewName, $OldLower -> $NewLower)" -ForegroundColor Cyan

$changedFiles = 0
Get-ChildItem -Recurse -File | Where-Object {
    -not (Test-SkipPath $_.FullName) -and (Test-TextFile $_) -and $_.Name -ne $ScriptName
} | ForEach-Object {
    # The repo is UTF-8 without BOM; the default Get-Content/Set-Content encoding depends on the machine's code page.
    $content = [IO.File]::ReadAllText($_.FullName, $Utf8NoBom)
    if ($content -cmatch [regex]::Escape($Old) -or $content -cmatch [regex]::Escape($OldLower)) {
        $updated = $content -creplace [regex]::Escape($Old), $NewName -creplace [regex]::Escape($OldLower), $NewLower
        [IO.File]::WriteAllText($_.FullName, $updated, $Utf8NoBom)
        $rel = $_.FullName.Substring($PWD.Path.Length + 1)
        Write-Host "    updated: $rel" -ForegroundColor DarkGray
        $changedFiles++
    }
}
Write-Host "    total files updated: $changedFiles" -ForegroundColor DarkGray

# --------------------------------------------------- renaming files/folders
Write-Host "`n==> Renaming files and folders" -ForegroundColor Cyan

# Files first (all at once), then folders deepest-first — so renaming a parent
# folder doesn't invalidate paths we already computed.
Get-ChildItem -Recurse -File |
    Where-Object { -not (Test-SkipPath $_.FullName) -and $_.Name -cmatch [regex]::Escape($Old) } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $newFileName = $_.Name -creplace [regex]::Escape($Old), $NewName
        Rename-Item -Path $_.FullName -NewName $newFileName
        Write-Host "    file:   $($_.Name)  ->  $newFileName" -ForegroundColor DarkGray
    }

Get-ChildItem -Recurse -Directory |
    Where-Object { -not (Test-SkipPath $_.FullName) -and $_.Name -cmatch [regex]::Escape($Old) } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $newDirName = $_.Name -creplace [regex]::Escape($Old), $NewName
        Rename-Item -Path $_.FullName -NewName $newDirName
        Write-Host "    folder: $($_.Name)  ->  $newDirName" -ForegroundColor DarkGray
    }

# ------------------------------------------- this script's own .slnx entry
Write-Host "`n==> Removing $ScriptName from the solution items" -ForegroundColor Cyan

Get-ChildItem -File -Filter '*.slnx' | ForEach-Object {
    [string[]]$lines = [IO.File]::ReadAllLines($_.FullName, $Utf8NoBom)
    [string[]]$kept = @($lines | Where-Object { $_ -notmatch [regex]::Escape($ScriptName) })
    if ($kept.Count -ne $lines.Count) {
        [IO.File]::WriteAllLines($_.FullName, $kept, $Utf8NoBom)
        Write-Host "    removed from $($_.Name)" -ForegroundColor DarkGray
    }
}

# ------------------------------------------------------------- leftovers
Write-Host "`n==> Checking for a leftover '$Old' or '$OldLower'" -ForegroundColor Cyan

$binaryExtensions = @('.png', '.ico', '.jpg', '.jpeg', '.gif', '.webp', '.woff', '.woff2', '.ttf', '.dll', '.exe', '.pdb', '.nupkg', '.zip')
$leftovers = @(Get-ChildItem -Recurse -File |
    Where-Object { -not (Test-SkipPath $_.FullName) -and $binaryExtensions -notcontains $_.Extension -and $_.Name -ne $ScriptName } |
    Select-String -CaseSensitive -Pattern ([regex]::Escape($Old) + '|' + [regex]::Escape($OldLower)))

if ($leftovers.Count -gt 0) {
    foreach ($hit in $leftovers) {
        Write-Host "    $($hit.Path.Substring($PWD.Path.Length + 1)):$($hit.LineNumber)" -ForegroundColor Yellow
    }
    Write-Warning "$($leftovers.Count) line(s) still mention the template name (a file type this script doesn't rewrite) - fix them by hand."
} else {
    Write-Host "    none" -ForegroundColor DarkGray
}

# ------------------------------------------------------------------- done
Write-Host "`n=================================================" -ForegroundColor Green
Write-Host " Done. Next steps:" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host @"
  1. dotnet build                 (make sure everything still builds)
  2. Review docker-compose.yml and connection strings
     (database name is now '$NewLower')
  3. Delete this $ScriptName (its .slnx entry is already gone)
  4. git add -A
     git commit -m "chore: rename template to $NewName"
"@
