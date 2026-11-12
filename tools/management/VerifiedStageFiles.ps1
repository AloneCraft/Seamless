function Get-FileIdentity([string[]]$paths){
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($path in $paths | Sort-Object -Unique){
        if(![IO.Path]::IsPathFullyQualified($path)){throw "Input requires an absolute path: $path"}
        $item=Get-Item -LiteralPath $path -Force
        $entries=@($item)
        if($item.PSIsContainer){$entries+=@(Get-ChildItem -LiteralPath $item.FullName -Recurse -Force)}
        foreach($entry in $entries | Sort-Object FullName){
            if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "A linked input cannot be sealed: $($entry.FullName)"}
            if(!$entry.PSIsContainer -and $seen.Add($entry.FullName)){
                [ordered]@{Path=$entry.FullName;Sha256=(Get-FileHash -LiteralPath $entry.FullName).Hash}
            }
        }
    }
}

function Get-ArtifactIdentityHash([object[]]$files){
    $orderedFiles=@($files|Sort-Object {$_.Path})
    $json=ConvertTo-Json -InputObject $orderedFiles -Depth 3 -Compress
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json)))
}
