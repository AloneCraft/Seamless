[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishedDirectory,
    [Parameter(Mandatory)][string]$ReferenceDirectory,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$RegressionStage
)

$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd([IO.Path]::DirectorySeparatorChar)
$published=(Resolve-Path -LiteralPath $PublishedDirectory).ProviderPath
$reference=(Resolve-Path -LiteralPath $ReferenceDirectory).ProviderPath
$output=[IO.Path]::GetFullPath($OutputPath)
if(!$output.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -and !$output.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPathはリポジトリ内かF:\Seamless配下を指定してください。'}
if(Test-Path -LiteralPath $output){throw '既存の照合記録は上書きしません。'}

function Read-AssemblySource([string]$PdbPath){
    $stream=[IO.File]::OpenRead($PdbPath);$provider=$null
    try{
        $provider=[Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream)
        $reader=$provider.GetMetadataReader()
        [byte[]]$identifier=@($reader.DebugMetadataHeader.Id)
        if($identifier.Length -ne 20){throw "Invalid portable PDB identifier: $PdbPath"}
        $documents=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
        $generated=0
        foreach($handle in $reader.Documents){
            $document=$reader.GetDocument($handle);$path=$reader.GetString($document.Name)
            if(!$path.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $document.Hash.IsNil){continue}
            $relative=[IO.Path]::GetRelativePath($repository,$path).Replace('\','/')
            if($relative -match '(^|/)(obj|bin)/'){$generated++;continue}
            $algorithm=switch($reader.GetGuid($document.HashAlgorithm).ToString()){
                '8829d00f-11b8-4213-878b-770e8597ac16' {'SHA256'}
                'ff1816ec-aa5e-4d10-87f7-6f4963833460' {'SHA1'}
                default {throw "Unsupported PDB checksum algorithm: $PdbPath"}
            }
            $documents.Add($relative,[pscustomobject]@{Algorithm=$algorithm;Hash=[Convert]::ToHexString($reader.GetBlobBytes($document.Hash))})
        }
        [pscustomobject]@{Id=[guid]::new([byte[]]$identifier[0..15]);Stamp=[BitConverter]::ToUInt32($identifier,16);Documents=$documents;GeneratedDocuments=$generated}
    }finally{if($provider){$provider.Dispose()};$stream.Dispose()}
}

function Assert-AssemblyPdb([string]$DllPath,$Pdb){
    $stream=[IO.File]::OpenRead($DllPath);$reader=$null
    try{
        $reader=[Reflection.PortableExecutable.PEReader]::new($stream)
        $matches=@(foreach($entry in $reader.ReadDebugDirectory()){
            if($entry.Type -ne [Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView){continue}
            $data=$reader.ReadCodeViewDebugDirectoryData($entry)
            if($data.Guid -eq $Pdb.Id -and $data.Age -eq 1 -and $entry.Stamp -eq $Pdb.Stamp){$entry}
        })
        if($matches.Count -ne 1){throw "DLL/PDB identity mismatch: $DllPath"}
    }finally{if($reader){$reader.Dispose()};$stream.Dispose()}
}

function Read-RegressionStage([string]$path){
    $inputPath=Join-Path $path 'stage-input.json'
    $proofPath=Join-Path $path 'stage-passed.json'
    $proof=Get-Content -LiteralPath $proofPath -Raw|ConvertFrom-Json
    $inputHash=(Get-FileHash -LiteralPath $inputPath).Hash
    $inputRecord=Get-Content -LiteralPath $inputPath -Raw|ConvertFrom-Json
    if($inputRecord.Format -ne 3 -or $proof.Outcome -ne 'Passed' -or $proof.Passed -lt 1 -or $proof.InputSha256 -cne $inputHash){
        throw 'The regression must have a passing record bound to its sealed inputs.'
    }
    $arguments=$inputRecord.Arguments
    if($arguments.Count -lt 2 -or $arguments[0] -cne 'vstest' -or
        ![IO.Path]::IsPathFullyQualified($arguments[1]) -or [IO.Path]::GetExtension($arguments[1]) -ne '.dll'){
        throw 'Distribution provenance requires a fixed VSTest assembly.'
    }
    $testDirectory=[IO.Path]::GetDirectoryName($arguments[1])
    if($reference -ne $testDirectory -and $reference -ne $inputRecord.ManagementUiDirectory){
        throw 'The reference directory was not used by the passing regression.'
    }
    $evidence=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $proof.Evidence){
        if(!$file.Path.StartsWith($path+'\',[StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $file.Path).Hash -cne $file.Sha256){
            throw "The regression evidence changed: $($file.Path)"
        }
        $evidence.Add($file.Path,$file.Sha256)
    }
    foreach($name in 'receipt.json','process-start.json','checks.trx'){
        if(!$evidence.ContainsKey((Join-Path $path $name))){throw "Missing sealed regression evidence: $name"}
    }
    $inputs=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $inputRecord.Inputs){$inputs.Add($file.Path,$file.Sha256)}
    if(!$inputs.ContainsKey($arguments[1])){throw 'The test assembly is missing from the sealed inputs.'}
    [pscustomobject]@{Directory=$path;InputSha256=$inputHash;ProofSha256=(Get-FileHash -LiteralPath $proofPath).Hash;Inputs=$inputs}
}

function Get-ReferenceHash([string]$file,$regression){
    $hash=(Get-FileHash -LiteralPath $file).Hash
    if($regression -and (!$regression.Inputs.ContainsKey($file) -or $regression.Inputs[$file] -cne $hash)){
        throw "Reference file was not sealed by the passing regression: $file"
    }
    return $hash
}

$projectDlls=@(foreach($deps in Get-ChildItem -LiteralPath $published -Filter *.deps.json -File){
    $document=Get-Content -LiteralPath $deps.FullName -Raw | ConvertFrom-Json
    foreach($target in $document.targets.PSObject.Properties){foreach($entry in $target.Value.PSObject.Properties){
        if($document.libraries.PSObject.Properties[$entry.Name].Value.type -ne 'project'){continue}
        foreach($asset in $entry.Value.runtime.PSObject.Properties.Name){if($asset -and [IO.Path]::GetExtension($asset) -eq '.dll'){[IO.Path]::GetFileName($asset)}}
    }}
})
$projectDlls=@($projectDlls | Sort-Object -Unique)
if(!$projectDlls.Count){throw 'Project runtime assemblies were not found in the published deps.json.'}
$regression=$null
if($RegressionStage){
    $stage=(Resolve-Path -LiteralPath $RegressionStage).ProviderPath.TrimEnd('\')
    $regression=Read-RegressionStage $stage
}
$unverified=[Collections.Generic.List[string]]::new()
$assemblies=@(foreach($name in $projectDlls){
    $pdb=Get-Item -LiteralPath (Join-Path $published ([IO.Path]::GetFileNameWithoutExtension($name)+'.pdb'))
    $candidate=Read-AssemblySource $pdb.FullName
    if(!$candidate.Documents.Count){$unverified.Add($name);continue}
    $baselinePdb=Join-Path $reference $pdb.Name
    $baseline=Read-AssemblySource $baselinePdb
    $candidateDll=Join-Path $published $name;$baselineDll=Join-Path $reference $name
    Assert-AssemblyPdb $candidateDll $candidate;Assert-AssemblyPdb $baselineDll $baseline
    if($candidate.Documents.Count -ne $baseline.Documents.Count){throw "Source document count differs: $name"}
    foreach($file in $candidate.Documents.Keys){
        if(!$baseline.Documents.ContainsKey($file) -or $candidate.Documents[$file].Algorithm -ne $baseline.Documents[$file].Algorithm -or $candidate.Documents[$file].Hash -ne $baseline.Documents[$file].Hash){throw "Reference source mismatch: $name / $file"}
        if((Get-FileHash -LiteralPath (Join-Path $repository $file) -Algorithm $candidate.Documents[$file].Algorithm).Hash -ne $candidate.Documents[$file].Hash){throw "Current source mismatch: $file"}
    }
    [pscustomobject]@{Assembly=$name;SourceDocuments=$candidate.Documents.Count;GeneratedDocumentsExcluded=$candidate.GeneratedDocuments;PublishedDllSha256=(Get-FileHash -LiteralPath $candidateDll).Hash;ReferenceDllSha256=Get-ReferenceHash $baselineDll $regression;PublishedPdbSha256=(Get-FileHash -LiteralPath $pdb.FullName).Hash;ReferencePdbSha256=Get-ReferenceHash $baselinePdb $regression}
})
if(!$assemblies.Count){throw '照合できるリポジトリソースを持つPDBがありません。'}
$stageRecord=$null
if($regression){$stageRecord=[ordered]@{Directory=$regression.Directory;InputSha256=$regression.InputSha256;ProofSha256=$regression.ProofSha256}}
[ordered]@{PublishedDirectory=$published;ReferenceDirectory=$reference;RegressionStage=$stageRecord;CheckedUtc=[DateTime]::UtcNow.ToString('o');ProjectRuntimeAssemblies=$projectDlls;Assemblies=$assemblies;UnverifiedProjectAssemblies=@($unverified.ToArray());Scope='Project DLL/PDB identity and ordinary repository source checksums; virtual generated documents and NuGet package sources excluded. This does not replace packaged execution tests.'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $output -Encoding utf8
Write-Output ("DLL/PDB identity and repository sources verified: "+$assemblies.Count+" assemblies")
Write-Output $output
