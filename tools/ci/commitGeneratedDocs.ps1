# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Commit a generated documentation to the gh-pages documentation branch
# For the master branch, this commits the documentation at the root of
# the branch. For other branches, this commits it under the versions/ folder.

param (
    [string]$StagingFolder
)

# Return the directory containing this script
function Get-ScriptDirectory {
    $Invocation = (Get-Variable MyInvocation -Scope 1).Value
    Split-Path $Invocation.MyCommand.Path
}

. "$(Get-ScriptDirectory)\utils.ps1"

# Create some authentication tokens to be able to connect to Azure DevOps to get changes and to GitHub to push changes
Write-Host "Create auth tokens to connect to GitHub and Azure DevOps"
$Authorization = "Basic " + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("${env:GITHUB_USER}:${env:GITHUB_PAT}"))

# Get info about last change associated with the new generated docs
Write-Host "Get the SHA1 and title of the most recent commit"
$commitSha = git log -1 --pretty=%H
$commitTitle = git log -1 --pretty=%s
Write-Host "${commitSha}: $commitTitle"

# Clean the staging folder to avoid any interaction with a previous build if the agent is not clean
Write-Host "Clean staging folder if it exists"
Ensure-Empty $StagingFolder

$output = ""
Invoke-Expression "git rev-parse --verify `"refs/remotes/origin/gh-pages^{commit}`"" | Tee-Object -Variable output | Out-Null
if (-not $output) {
    Write-Host "Missing docs branch 'gh-pages'"
    Write-Host "##vso[task.complete result=Failed;]Missing docs branch 'gh-pages'."
    exit 1
}
Write-Host "Destination folder: $StagingFolder"

# Clone the destination branch locally in a temporary folder.
# This will be used to only commit changes to that gh-pages branch which
# contains only generated documentation-related files, and not the code.
# Note that we always clone into $StagingFolder, which is the repository root,
# even if the destination folder is a sub-folder.
Write-Host "Clone the generated docs branch"
$cloneCommand = "git -c http.extraheader=""AUTHORIZATION: $Authorization"" clone https://github.com/Microsoft/MixedReality-Sharing.git --branch gh-pages --no-checkout ""$StagingFolder"""
# Pass a custom display name so that credentials are not printed out in case of error.
Invoke-NoFailOnStdErr $cloneCommand -DisplayName "git clone ..."

# Copy the newly-generated version of the docs
Write-Host "Copy new generated version"
Copy-Item ".\build\docs\generated\*" -Destination $StagingFolder -Force -Recurse

# Move inside the generated docs repository, so that subsequent git commands
# apply to this repo/branch and not the global one with the source code.
Write-Host "Move to $StagingFolder"
Push-Location $StagingFolder

try {
    # Set author for the generated docs commit
    Write-Host "Set docs commit author to '${env:GITHUB_NAME} <${env:GITHUB_EMAIL}>'"
    git config user.name ${env:GITHUB_NAME}
    git config user.email ${env:GITHUB_EMAIL}

    # After "git clone --no-checkout" the index contains all files as deleted, clear it
    git reset

    # Check for any change compared to previous version (if any)
    Write-Host "Check for changes"
    $statusCommand = "git status --short"
    $statusOut = Invoke-NoFailOnStdErr $statusCommand
    if ($statusOut) {
        # Add everything. Because the current directory is $StagingFolder, this is everything from
        # the point of view of the sub-repo inside $StagingFolder, so this ignores all changes outside
        # this directory and retain only generated docs changes, which is exactly what we want.
        Invoke-NoFailOnStdErr "git add --all"
        Invoke-NoFailOnStdErr "git commit -m ""Generated docs for commit $commitSha ($commitTitle)"""
        Invoke-NoFailOnStdErr "git -c http.extraheader=""AUTHORIZATION: $Authorization"" push origin ""$DestBranch""" -DisplayName "git push..."
        Write-Host "Docs changes committed"
    } else {
        Write-Host "Docs are up to date"
    }
} finally {
    Pop-Location
}
