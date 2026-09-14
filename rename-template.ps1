<#
    Renames "AppTemplate" to your new project's name — both in file contents
    and in the names of files/folders that contain "AppTemplate".

    Run this RIGHT AFTER "Use this template" on the new repo, before the
    first commit in that new project.

    Usage (from the repo root):
        powershell -ExecutionPolicy Bypass -File .\rename-template.ps1 -NewName Slicice

    The script is idempotent: running it twice just finds no more "AppTemplate"
    the second time, so nothing changes.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$NewName
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$Old      = 'AppTemplate'
$OldLower = 'apptemplate'
$NewLower = $NewName.ToLower()

if ($NewName -notmatch '^[A-Z][A-Za-z0-9]*$') {
    Write-Warning "'$NewName' doesn't look like PascalCase (e.g. 'Slicice', 'PackageTracker'). Continuing anyway, but double-check namespaces afterwards."
}

$excludeDirs = @('.git', 'bin', 'obj', 'node_modules', '.vs')

function Test-SkipPath([string]$path) {
    $segments = $path -split '[\\/]'
    foreach ($seg in $excludeDirs) {
        if ($segments -contains $seg) { return $true }
    }
    return $false
}

# ------------------------------------------------------------- file contents
Write-Host "`n==> Replacing file contents ($Old -> $NewName, $OldLower -> $NewLower)" -ForegroundColor Cyan

$textExtensions = @(
    '.cs', '.csproj', '.sln', '.slnx', '.props', '.targets', '.json',
    '.md', '.yml', '.yaml', '.ps1', '.config', '.gitattributes'
)
$textNamesNoExt = @('.gitignore', '.editorconfig')

$changedFiles = 0
Get-ChildItem -Recurse -File | Where-Object {
    -not (Test-SkipPath $_.FullName) -and
    ($textExtensions -contains $_.Extension -or $textNamesNoExt -contains $_.Name)
} | ForEach-Object {
    $content = Get-Content -Path $_.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { return }
    if ($content -match [regex]::Escape($Old) -or $content -match [regex]::Escape($OldLower)) {
        $updated = $content -replace [regex]::Escape($Old), $NewName -replace [regex]::Escape($OldLower), $NewLower
        Set-Content -Path $_.FullName -Value $updated -NoNewline
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
    Where-Object { -not (Test-SkipPath $_.FullName) -and $_.Name -match [regex]::Escape($Old) } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $newFileName = $_.Name -replace [regex]::Escape($Old), $NewName
        Rename-Item -Path $_.FullName -NewName $newFileName
        Write-Host "    file:   $($_.Name)  ->  $newFileName" -ForegroundColor DarkGray
    }

Get-ChildItem -Recurse -Directory |
    Where-Object { -not (Test-SkipPath $_.FullName) -and $_.Name -match [regex]::Escape($Old) } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $newDirName = $_.Name -replace [regex]::Escape($Old), $NewName
        Rename-Item -Path $_.FullName -NewName $newDirName
        Write-Host "    folder: $($_.Name)  ->  $newDirName" -ForegroundColor DarkGray
    }

# ------------------------------------------------------------------- done
Write-Host "`n=================================================" -ForegroundColor Green
Write-Host " Done. Next steps:" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host @"
  1. dotnet build                 (make sure everything still builds)
  2. Review docker-compose.yml and connection strings
     (database name is now '$NewLower')
  3. Delete this rename-template.ps1 (no longer needed in this project)
  4. git add -A
     git commit -m "chore: rename template to $NewName"
"@
