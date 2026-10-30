param(
    [Parameter(Mandatory=$true)][string]$ManifestPath,
    [string]$OutputDirectory=$PSScriptRoot
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0

function ConvertTo-SqlIdentifier([string]$Value) {
    return '[' + $Value.Replace(']',']]') + ']'
}

function ConvertTo-FileSegment([string]$Value) {
    $segment=[regex]::Replace($Value,'[^A-Za-z0-9_.-]','_')
    $segment=$segment.Trim('.','_')
    if([string]::IsNullOrWhiteSpace($segment)) { $segment='unnamed' }
    $sha=[System.Security.Cryptography.SHA256]::Create()
    try {
        $digest=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-','').Substring(0,8).ToLowerInvariant()
    } finally { $sha.Dispose() }
    return $segment + '-' + $digest
}

function Add-Element([System.Xml.XmlDocument]$Document,[System.Xml.XmlNode]$Parent,[string]$Name,[string]$Text=$null) {
    $element=$Document.CreateElement($Name)
    if($null -ne $Text) { $element.InnerText=$Text }
    [void]$Parent.AppendChild($element)
    return $element
}

function Add-ProjectConnection([System.Xml.XmlDocument]$Document,[System.Xml.XmlNode]$Items,[string]$Server,[string]$Database,[string]$Authentication,[string]$UserName) {
    $node=$Document.CreateElement('ConnectionNode')
    $node.SetAttribute('Name',($Server + ':' + $Database + $(if($UserName){':' + $UserName}else{''})))
    [void]$Items.AppendChild($node)
    [void](Add-Element $Document $node 'Created' '2000-01-01T00:00:00Z')
    [void](Add-Element $Document $node 'Type' 'SQL')
    [void](Add-Element $Document $node 'Server' $Server)
    [void](Add-Element $Document $node 'UserName' $UserName)
    [void](Add-Element $Document $node 'Authentication' $Authentication)
    [void](Add-Element $Document $node 'InitialDB' $Database)
    [void](Add-Element $Document $node 'LoginTimeout' '30')
    [void](Add-Element $Document $node 'ExecutionTimeout' '0')
    [void](Add-Element $Document $node 'ConnectionProtocol' 'NotSpecified')
    [void](Add-Element $Document $node 'ApplicationName' 'Microsoft SQL Server Management Studio - Query')
}

function Add-ProjectFile([System.Xml.XmlDocument]$Document,[System.Xml.XmlNode]$Items,[string]$Name,[string]$RelativePath,[string]$Server,[string]$UserName,[bool]$WindowsAuthentication) {
    $node=$Document.CreateElement('FileNode')
    $node.SetAttribute('Name',$Name)
    [void]$Items.AppendChild($node)
    $moniker='8c91a03d-f9b4-46c0-a305-b5dcc79ff907:' + $Server + ':' + $(if($WindowsAuthentication){'True'}else{'False:' + $UserName})
    [void](Add-Element $Document $node 'AssociatedConnectionMoniker' $moniker)
    [void](Add-Element $Document $node 'AssociatedConnSrvName' $Server)
    [void](Add-Element $Document $node 'AssociatedConnUserName' $UserName)
    [void](Add-Element $Document $node 'FullPath' $RelativePath)
}

function New-SqlProject([string]$ProjectDirectory,[string]$ProjectName,[object[]]$Databases,[string]$Server,[bool]$WindowsAuthentication,[string]$UserName,[string]$Side) {
    $document=New-Object System.Xml.XmlDocument
    $declaration=$document.CreateXmlDeclaration('1.0','utf-8',$null)
    [void]$document.AppendChild($declaration)
    $root=$document.CreateElement('SqlWorkbenchSqlProject')
    $root.SetAttribute('xmlns:xsi','http://www.w3.org/2001/XMLSchema-instance')
    $root.SetAttribute('xmlns:xsd','http://www.w3.org/2001/XMLSchema')
    $root.SetAttribute('Name',$ProjectName)
    [void]$document.AppendChild($root)
    $rootItems=Add-Element $document $root 'Items'

    $connections=Add-Element $document $rootItems 'LogicalFolder'
    $connections.SetAttribute('Name','Connections'); $connections.SetAttribute('Type','2'); $connections.SetAttribute('Sorted','true')
    $connectionItems=Add-Element $document $connections 'Items'
    $connectionDatabases=@($Databases)
    if(@($connectionDatabases | Where-Object { [string]$_.Name -eq 'Seamless_Comparison_Fixture' }).Count -eq 0) {
        $connectionDatabases+= [pscustomobject]@{Name='Seamless_Comparison_Fixture'}
    }
    foreach($database in $connectionDatabases) {
        Add-ProjectConnection $document $connectionItems $Server ([string]$database.Name) $(if($WindowsAuthentication){'Windows Authentication'}else{'SQL'}) $UserName
    }

    $queries=Add-Element $document $rootItems 'LogicalFolder'
    $queries.SetAttribute('Name','Queries'); $queries.SetAttribute('Type','0'); $queries.SetAttribute('Sorted','true')
    $queryItems=Add-Element $document $queries 'Items'
    foreach($caseName in @('Read-AggregateJoinView.sql','Fixture-CRUD-CommitRollback.sql')) {
        $relative='..\..\Cases\Common\' + $caseName
        Add-ProjectFile $document $queryItems $caseName $relative $Server $UserName $WindowsAuthentication
    }
    foreach($database in $Databases) {
        $databaseName=[string]$database.Name
        $databaseSegment=ConvertTo-FileSegment $databaseName
        foreach($schema in @($database.Catalog.Schemas)) {
            $schemaName=[string]$schema.Name
            $schemaSegment=ConvertTo-FileSegment $schemaName
            foreach($kind in @('Tables','Views')) {
                foreach($item in @($schema.$kind)) {
                    $objectName=if($item -is [string]){[string]$item}else{[string]$item.Name}
                    if([string]::IsNullOrWhiteSpace($objectName)){continue}
                    $objectSegment=ConvertTo-FileSegment $objectName
                    $kindLabel=if($kind -eq 'Tables'){'Table'}else{'View'}
                    $relative='Generated\' + $databaseSegment + '\' + $schemaSegment + '\' + $kindLabel + '-' + $objectSegment + '.sql'
                    $outputPath=Join-Path $ProjectDirectory $relative
                    $outputParent=Split-Path -Parent $outputPath
                    [void](New-Item -ItemType Directory -Force -Path $outputParent)
                    $sql='-- Generated from the rebuild manifest. Do not edit this generated file.' + "`r`n" + 'SELECT TOP (100) * FROM ' + (ConvertTo-SqlIdentifier $databaseName) + '.' + (ConvertTo-SqlIdentifier $schemaName) + '.' + (ConvertTo-SqlIdentifier $objectName) + ';' + "`r`n"
                    if((Test-Path -LiteralPath $outputPath -PathType Leaf) -and -not ((Get-Content -LiteralPath $outputPath -TotalCount 1) -like '-- Generated from the rebuild manifest*')) {
                        throw "既存の手書きファイルは保護されました: $outputPath"
                    }
                    [System.IO.File]::WriteAllText($outputPath,$sql,(New-Object System.Text.UTF8Encoding($false)))
                    $relativePath=$relative.Replace('/','\')
                    Add-ProjectFile $document $queryItems ((Split-Path -Leaf $relativePath)) $relativePath $Server $UserName $WindowsAuthentication
                }
            }
        }
    }

    $misc=Add-Element $document $rootItems 'LogicalFolder'
    $misc.SetAttribute('Name','Miscellaneous'); $misc.SetAttribute('Type','3'); $misc.SetAttribute('Sorted','true')
    [void](Add-Element $document $misc 'Items')

    $projectPath=Join-Path $ProjectDirectory ($ProjectName + '.ssmssqlproj')
    $settings=New-Object System.Xml.XmlWriterSettings
    $settings.Encoding=New-Object System.Text.UTF8Encoding($false)
    $settings.Indent=$true
    $writer=[System.Xml.XmlWriter]::Create($projectPath,$settings)
    try { $document.Save($writer) } finally { $writer.Dispose() }
}

if(-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw "manifestがありません: $ManifestPath" }
$sourceConnectionString=[Environment]::GetEnvironmentVariable('ConnectionStrings__Source')
if([string]::IsNullOrWhiteSpace($sourceConnectionString)) { throw 'ConnectionStrings__Source を設定してください。' }
$serverMatch=[regex]::Match($sourceConnectionString,'(?i)(?:^|;)\s*(?:data\s+source|server|address|addr|network\s+address)\s*=\s*([^;]+)')
$sourceServer=if($serverMatch.Success){$serverMatch.Groups[1].Value.Trim().Trim([char[]]@(123,125))}else{''}
if($sourceServer -notin @('localhost','tcp:localhost')) { throw 'ConnectionStrings__Source の接続先は localhost である必要があります。' }
if($sourceConnectionString -notmatch '(?i)(?:^|;)\s*(?:integrated\s+security|trusted_connection)\s*=\s*(?:true|yes|sspi)(?:;|$)') { throw 'ConnectionStrings__Source は Windows 統合認証を使ってください。' }
if($sourceConnectionString -match '(?i)(?:^|;)\s*(?:password|pwd)\s*=') { throw 'ConnectionStrings__Source に資格情報を含めないでください。' }

$manifest=Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$databases=@($manifest.Databases)
if($databases.Count -eq 0) { throw 'manifest.Databases が空です。' }
foreach($database in $databases) {
    if([string]::IsNullOrWhiteSpace([string]$database.Name)) { throw 'manifest.Databases[].Name が空です。' }
    if($null -eq $database.Catalog -or $null -eq $database.Catalog.Schemas) { throw "Catalog.Schemas がありません: $($database.Name)" }
}
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
$sqlServerProject=Join-Path $outputRoot 'SQLServer'
$seamlessProject=Join-Path $outputRoot 'SeamlessDB'
[void](New-Item -ItemType Directory -Force -Path $sqlServerProject)
[void](New-Item -ItemType Directory -Force -Path $seamlessProject)
New-SqlProject $sqlServerProject 'SQLServer' $databases $sourceServer $true '' 'SQLServer'
New-SqlProject $seamlessProject 'SeamlessDB' $databases 'localhost,14333' $false 'sa' 'SeamlessDB'
Write-Output "SSMS project files generated from manifest: $($databases.Count) databases; passwords were not written."
