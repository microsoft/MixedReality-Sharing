# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Silence errors to avoid PS failing when git commands write to stderr.
# See https://github.com/microsoft/azure-pipelines-yaml/issues/306
function Invoke-NoFailOnStdErr($Command, $DisplayName = $Command) {
    $oldEAPreference = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"

    $errorHasOccurred = $False
    try {
        $out = Invoke-Expression $Command -ErrorVariable $errorMessage
        $errorHasOccurred = $LastExitCode -ne 0
    } catch {
        $errorHasOccurred = $True
        $errorMessage = $_
    } finally {
        $ErrorActionPreference = $oldEAPreference
    }

    if ($errorHasOccurred) {
        $msg = "Command '$DisplayName' returned an error"
        Write-Host $out
        Write-Host $errorMessage
        Write-Host "##vso[task.complete result=Failed;]$msg"
        throw $msg
    }
    return $out
}

# Ensure that the directory at the given path exists and is empty.
function Ensure-Empty($DirectoryPath) {
    mkdir -Force $DirectoryPath | out-null
    Remove-Item "$DirectoryPath\*" -Force -Recurse
}