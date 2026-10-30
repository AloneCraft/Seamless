param([Parameter(Mandatory)][string]$FlatDirectory,[Parameter(Mandatory)][string]$ManifestPath,[Parameter(Mandatory)][string]$GenerationSpec,[Parameter(Mandatory)][string]$GenerationReceipt,[Parameter(Mandatory)][string]$PreflightResult)
$ErrorActionPreference='Stop'
function Get-FileHash([Parameter(Mandatory)][string]$LiteralPath,[string]$Algorithm='SHA256') {
    if($Algorithm -ne 'SHA256'){throw 'Only SHA256 is supported.'}
    $stream=[IO.File]::OpenRead($LiteralPath);$hash=[Security.Cryptography.SHA256]::Create()
    try{return [pscustomobject]@{Hash=[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')}}finally{$hash.Dispose();$stream.Dispose()}
}

if(Test-Path -LiteralPath $ManifestPath) {throw 'Manifest already exists; refusing to replace evidence.'}
$spec=Get-Content -Raw -Encoding UTF8 $GenerationSpec|ConvertFrom-Json
$receipt=Get-Content -Raw -Encoding UTF8 $GenerationReceipt|ConvertFrom-Json
$argsList=@($spec.arguments)
if($receipt.status -ne 'Succeeded' -or $receipt.exitCode -ne 0 -or $receipt.operation -ne $spec.operation -or $receipt.executable -ne $spec.executable) {throw 'Generation receipt does not match a successful operation.'}
if(-not (Select-String -LiteralPath (Join-Path $receipt.artifactDirectory 'stdout.log') -Pattern '^EGen v1\.14\.0' -Quiet)) {throw 'Generator did not report version 1.14.0.'}
if(($argsList[0..9] -join ' ') -ne '-b 1 -c 1000 -t 1000 -f 500 -w 10' -or $argsList[10] -ne '-i' -or $argsList[12] -ne '-o' -or $argsList[14] -ne '-x') {throw 'Generation arguments differ from 1000 customers, 10 days, scale factor 500.'}
if([IO.Path]::GetFullPath($argsList[13]).TrimEnd('\') -ne [IO.Path]::GetFullPath($FlatDirectory).TrimEnd('\')) {throw 'Generation output directory differs.'}
$preflight=Get-Content -Raw -Encoding UTF8 $PreflightResult|ConvertFrom-Json
if($preflight.counts.CUSTOMER -ne 1000 -or $preflight.counts.TRADE -ne 576000 -or @($preflight.counts.PSObject.Properties).Count -ne 33) {throw 'Preflight counts differ from required data.'}
$inputHashes=@{}; Get-ChildItem -File -LiteralPath $argsList[11]|Sort-Object Name|ForEach-Object {$inputHashes[$_.Name]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
$manifest=@{status='generated';customers=1000;days=10;scaleFactor=500;generatorVersion='1.14.0';generatorSha256=(Get-FileHash -LiteralPath $spec.executable -Algorithm SHA256).Hash;arguments=$argsList;generationTaskId=$receipt.id;generationSpecHash=$receipt.specHash;inputSha256=$inputHashes;outputSha256=$preflight.outputSha256;tableCounts=$preflight.counts}
$manifest|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 -LiteralPath $ManifestPath
