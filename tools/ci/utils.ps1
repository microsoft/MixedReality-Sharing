# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Silence errors to avoid PS failing when git commands write to stderr.
# See https://github.com/microsoft/azure-pipelines-yaml/issues/306
function Invoke-NoFailOnStdErr($Command, $DisplayName = $Command) {
    $oldEAPreference = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"

    $out = Invoke-Expression $Command -ErrorVariable err

    if ($LastExitCode -ne 0) {
        $ErrorActionPreference = $oldEAPreference
        $msg = "Command '$DisplayName' returned an error"
        Write-Host $msg
        Write-Host $out
        Write-Host $err
        Write-Host "##vso[task.complete result=Failed;]$msg"
        exit 1
    }
    $ErrorActionPreference = $oldEAPreference
    return $out
}

# Ensure that the directory at the given path exists and is empty.
function Ensure-Empty($DirectoryPath) {
    mkdir -Force $DirectoryPath | out-null
    Remove-Item "$DirectoryPath\*" -Force -Recurse
}