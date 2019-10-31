# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See LICENSE in the project root for license information.

# Script to generate the docs into build/docs/generated/ for local iteration.

param(
    # Serve the generated docs on a temporary web server @ localhost
    # The docs are not completely static, so will not work if not served.
    [switch]$serve = $false,
    $docfx_url = "https://github.com/dotnet/docfx/releases/download/v2.47/docfx.zip"
    #$docfx_url = #"https://osgwiki.com"
    #$docfx_url = "http://httpd.apache.org/docs/2.2/mod/mod_expires.html"
)

# Return the directory containing this script
function Get-ScriptDirectory {
    $Invocation = (Get-Variable MyInvocation -Scope 1).Value
    Split-Path $Invocation.MyCommand.Path
}

# TODO proper escapes
function FileNameFromUrl ($url) {
    ($url -replace "/", "_") -replace ":", "_"
}

# Download $url to local $cache_folder and return the cached path.
# Note that the cached path may have a long name such as https___example.com_path_to_download_v2.1_foo.zip
# This method uses ETag to avoid refetching, and the value is stored in a sidecar ".props" file
function FetchAndCache ($url, $cache_folder) {
    $cache_file = Join-Path $cache_folder (FileNameFromUrl $url)
    
    # headers to send with our request.
    $headers = @{}

    # props is a json file with keys "etag" and "size"(of the file)
    $props_text = Get-Content -ErrorAction Ignore "$cache_file.props" -raw
    if( $props_text ) {
        $props = ConvertFrom-Json -InputObject $props_text        
        $cur_size = (Get-Item -ErrorAction Ignore $cache_file).Length
        # Only add the etag if we pass some basic sanity checks
        if( $cur_size -and ($cur_size -eq $props.size) -and $props.etag) {
            $headers.Add("If-None-Match", $props.etag)
        }    
    }

    # do the fetch
    $OldProgressPreference = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue' # https://github.com/PowerShell/PowerShell/issues/2138
    Try {
        # Invoke-Webrequest has a bug which means $response.Content is wrong for binary files unless outfile is used
        $response = Invoke-WebRequest -Uri $url -Headers $headers -OutFile "$cache_file.tmp" -PassThru
    }
    Catch [System.Net.WebException] {
        if( $_.Exception.Response.StatusCode.value__ -eq 304) { # not modified
            Write-Host "Using cached version"
            return $cache_file
        }
        throw $_.Exception # any other error
    }
    finally {
        $ProgressPreference = $OldProgressPreference
    }
    
    # Fetched OK, move into place & update props
    Move-Item -Force "$cache_file.tmp" $cache_file
    $props = @{ size=$response.Content.Length; etag=($response.Headers["ETag"].Trim("`"")) }
    Set-Content -path "$cache_file.props" -value (ConvertTo-Json $props)

    return $cache_file
}

# Setup
$repo_root = Join-Path -resolve (Get-ScriptDirectory) ".."
$build_root = "$repo_root\build\docs"
$cache_root = "$repo_root\build\cache"
Write-Host "Repo root $repo_root, build root $build_root"

# Clear output dir
Write-Host "Clear previous version from $build_root"
Remove-Item -Force -Recurse -ErrorAction Ignore $build_root
mkdir $build_root | out-null
mkdir -ErrorAction Ignore $cache_root | out-null

# Fetch docfx
Write-Host "Fetching $docfx_url"
$docfx_zip = FetchAndCache $docfx_url $cache_root
Expand-Archive $docfx_zip -DestinationPath "$build_root\docfx"
$docfx_exe = "$build_root\docfx\docfx.exe"

# # Generate the documentation
Invoke-Expression "$docfx_exe docfx.json --intermediateFolder $build_root\obj -o $build_root $(if ($serve) {' --serve'} else {''})"
Write-Host "Documentation generated in $build_root/generated."

# # Clean-up obj/xdoc folders in source -- See https://github.com/dotnet/docfx/issues/1156
# $XdocDirs = Get-ChildItem -Path ..\libs -Recurse | Where-Object {$_.PSIsContainer -eq $true -and $_.Name -eq "xdoc"}
# foreach ($Xdoc in $XdocDirs)
# {
#     if ($Xdoc.Parent -match "obj")
#     {
#         Write-Host "Deleting $($Xdoc.FullName)"
#         Remove-Item -Force -Recurse -ErrorAction Ignore $Xdoc.FullName
#     }
# }



