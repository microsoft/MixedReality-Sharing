# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Silence errors to avoid PS failing when git commands write to stderr.
# See https://github.com/microsoft/azure-pipelines-yaml/issues/306
function Invoke-NoFailOnStdErr($Command, $DisplayName = $Command) {
    $oldEAPreference = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"

    try {
        $out = Invoke-Expression $Command
        # There seems to be no way to get the stderr output without the pipeline crashing,
        # so just return an error depending on the exit code.
        $code = $LastExitCode
        $errorHasOccurred = $code -ne 0
        $errorMessage = "Exit code: $code"
    } catch {
        $errorHasOccurred = $True
        $errorMessage = $_
    } finally {
        $ErrorActionPreference = $oldEAPreference
    }

    if ($errorHasOccurred) {
        if ($out) {
            Write-Host $out
        }
        $msg = "Command '$DisplayName' returned an error`n$errorMessage"
        Write-Host "##vso[task.complete result=Failed;]$msg"
        throw $msg
    }
    return $out
}

# Ensure that the directory at the given path exists and is empty.
function Ensure-Empty($DirectoryPath) {
    mkdir -Force "$DirectoryPath" | out-null
    Remove-Item "$DirectoryPath\*" -Force -Recurse
}