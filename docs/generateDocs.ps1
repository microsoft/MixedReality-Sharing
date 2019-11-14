# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Script to generate the docs into build/docs/generated/ for local iteration.

param(
    # Serve the generated docs on a temporary web server @ localhost
    # The docs are not completely static, so will not work if not served.
    [switch]$serve = $false,
    # DocFX pollutes the source folder with obj folders. We delete these
    # by default, but this breaks incremental builds. This option keeps
    # the https://github.com/dotnet/docfx/issues/1156
    [switch]$incremental = $false 
)

# Where to find the docfx binaries
$docfx_url = "https://github.com/dotnet/docfx/releases/download/v2.47/docfx.zip"

# Return the directory containing this script
function Get-ScriptDirectory {
    $Invocation = (Get-Variable MyInvocation -Scope 1).Value
    Split-Path $Invocation.MyCommand.Path
}

# TODO proper escapes
function FileNameFromUrl ($url) {
    $url -replace "[/:\?=%]", "_"
}

# https://github.com/PowerShell/PowerShell/issues/2138
# The progress bar can cause 50x slower performance :(
function Run-Quietly {
    param (
        [Parameter(Mandatory)]
        [Scriptblock] $Expression
    )

    $pp  = $global:ProgressPreference
    $global:ProgressPreference = 'SilentlyContinue'
    try {
        return $Expression.Invoke()
    }
    catch [System.Management.Automation.CmdletInvocationException] {
        throw $_.Exception.ErrorRecord.Exception
    }
    finally {
        $global:ProgressPreference = $pp
    }
}

# Download $url to local $cache_folder and return the cached path.
# Note that the cached path may have a long name such as https___example.com_path_to_download_v2.1_foo.zip
# This method uses ETag to avoid refetching, and the value is stored in a sidecar ".props" file
function FetchAndCache ($url, $cache_folder) {
    $cache_file = Join-Path $cache_folder (FileNameFromUrl $url)

    # headers to send with our request.
    $headers = @{}

    # props is a json file with keys "etag" and "size"(of the file)
    $props_text = Get-Content -ErrorAction Ignore -Path "$cache_file.props" -Raw
    if( $props_text ) {
        $props = ConvertFrom-Json -InputObject $props_text
        $cur_size = (Get-Item -ErrorAction Ignore $cache_file).Length
        # Only add the etag if we pass some basic sanity checks
        if( $cur_size -and ($cur_size -eq $props.size) -and $props.etag) {
            $headers.Add("If-None-Match", $props.etag)
        }
    }

    # do the web request
    Try {
        # Invoke-Webrequest has a bug which means $response.Content is wrong for binary files unless outfile is used
        $response = Run-Quietly { Invoke-WebRequest -Uri $url -Headers $headers -OutFile "$cache_file.tmp" -PassThru }
    }
    Catch [System.Net.WebException] {
        if( $_.Exception.Response.StatusCode.value__ -eq 304) { # etag matches, not modified
            return $cache_file, $true
        }
        throw $_.Exception # any other error
    }

    # Fetched OK, move into place & update props
    Move-Item -Force "$cache_file.tmp" $cache_file
    $props = @{ size=$response.Content.Length }
    $etag = ""
    if ($response.Headers.TryGetValue("ETag", [ref]$etag)) {
        $props.Add("etag", $etag.Trim("`""))
    }
    Set-Content -path "$cache_file.props" -value (ConvertTo-Json $props)

    return $cache_file, $false
}

# Setup
$repo_root = Join-Path -resolve (Get-ScriptDirectory) ".."
$build_root = "$repo_root\build\docs"
$cache_root = "$repo_root\build\cache"
Write-Host "Repo root $repo_root, build root $build_root"

# Clear output dir
if( $incremental -eq $false ) {
    Write-Host "Clear previous version from $build_root"
    Remove-Item -Force -Recurse $build_root
}

mkdir -ErrorAction Ignore $build_root | out-null
mkdir -ErrorAction Ignore $cache_root | out-null

# Fetch docfx
Write-Host "Fetching $docfx_url"
$docfx_zip, $cached = FetchAndCache $docfx_url $cache_root

$docfx_exe = "$cache_root\docfx\docfx.exe"
if( $cached -and (Test-Path -Path $docfx_exe -PathType leaf)) {
    Write-Host "Using cached $docfx_exe"
}
else {
    Write-Host "Unzipping $docfx_zip"
    Run-Quietly { Expand-Archive $docfx_zip -DestinationPath "$cache_root\docfx" }
}

# Generate the documentation
Invoke-Expression "$docfx_exe docfx.json --intermediateFolder $build_root\obj -o $build_root $(if ($serve) {' --serve --port 8081 '} else {''})"
Write-Host "Documentation generated in $build_root/generated."

# Clean-up obj/xdoc folders generated outside build tree -- See https://github.com/dotnet/docfx/issues/1156
if( $incremental -eq $false ) { # We need to keep these temp folders for incremental building
    $XdocDirs = Get-ChildItem -Path ..\libs -Recurse | Where-Object {$_.PSIsContainer -eq $true -and $_.Name -eq "xdoc"}
    foreach ($Xdoc in $XdocDirs) {
        if ($Xdoc.Parent -match "obj") {
            Write-Host "Deleting $($Xdoc.FullName)"
            Remove-Item -Force -Recurse -ErrorAction Ignore $Xdoc.FullName
        }
    }
}

