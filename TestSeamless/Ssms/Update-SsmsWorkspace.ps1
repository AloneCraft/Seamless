param(
    [string]$GenerationManifest,
    [ValidateSet('Update','Stage','Publish','Validate')][string]$Operation='Update',
    [string]$OutputDirectory=$PSScriptRoot,
    [string]$StagingDirectory,
    [string]$PublishDirectory=$PSScriptRoot,
    [string]$WindowsAuthenticationName
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

function Add-ProjectConnection([System.Xml.XmlDocument]$Document,[System.Xml.XmlNode]$Items,[string]$Server,[string]$Database,[string]$Authentication,[string]$UserName,[string]$EncryptionOption) {
    $node=$Document.CreateElement('ConnectionNode')
    $node.SetAttribute('Name',($Server + ':' + $Database + $(if($UserName){':' + $UserName}else{''})))
    [void]$Items.AppendChild($node)
    [void](Add-Element $Document $node 'Created' '2000-01-01T00:00:00Z')
    [void](Add-Element $Document $node 'Type' 'SQL')
    [void](Add-Element $Document $node 'Server' $Server)
    [void](Add-Element $Document $node 'UserName' $UserName)
    [void](Add-Element $Document $node 'Authentication' $Authentication)
    [void](Add-Element $Document $node 'EncryptionOption' $EncryptionOption)
    [void](Add-Element $Document $node 'TrustServerCertificate' 'True')
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
        Add-ProjectConnection $document $connectionItems $Server ([string]$database.Name) $(if($WindowsAuthentication){$WindowsAuthenticationName}else{'SQL'}) $UserName $(if($Side -ceq 'SeamlessDB'){'Optional'}else{'Mandatory'})
    }

    $queries=Add-Element $document $rootItems 'LogicalFolder'
    $queries.SetAttribute('Name','Queries'); $queries.SetAttribute('Type','0'); $queries.SetAttribute('Sorted','true')
    $queryItems=Add-Element $document $queries 'Items'
    foreach($caseName in @('Read-AggregateJoinView.sql','Fixture-CRUD-CommitRollback.sql')) {
        $relative=Get-RelativePath $script:PublishRoot (Join-Path $PSScriptRoot ('..\Cases\Common\'+$caseName))
        Add-ProjectFile $document $queryItems $caseName $relative $Server $UserName $WindowsAuthentication
    }
    $hasAdventureWorks=@($Databases | Where-Object { [string]$_.Name -ceq 'AdventureWorks2016' }).Count -gt 0
    if($hasAdventureWorks) {
        $relative=Get-RelativePath $script:PublishRoot (Join-Path $PSScriptRoot '..\Cases\AdventureWorks\Basic.sql')
        Add-ProjectFile $document $queryItems 'AdventureWorks Basic.sql' $relative $Server $UserName $WindowsAuthentication
        if($Side -ceq 'SeamlessDB') {
            $relative=Get-RelativePath $script:PublishRoot (Join-Path $PSScriptRoot '..\Cases\AdventureWorks\GeneratedMethods.sql')
            Add-ProjectFile $document $queryItems 'AdventureWorks GeneratedMethods.sql' $relative $Server $UserName $WindowsAuthentication
        }
    }
    foreach($database in $Databases) {
        $databaseName=[string]$database.Name
        $databaseSegment=ConvertTo-FileSegment $databaseName
        $catalogRelative='Generated\' + $databaseSegment + '\Catalog.sql'
        $catalogPath=Join-Path $ProjectDirectory $catalogRelative
        [void](New-Item -ItemType Directory -Force -Path (Split-Path -Parent $catalogPath))
        $databaseIdentifier=ConvertTo-SqlIdentifier $databaseName
        $catalogSql="-- Generated from the rebuild manifest. Do not edit this generated file.`r`n" + 'USE ' + $databaseIdentifier + ";`r`nGO`r`n" +
            "SELECT s.name AS schema_name,o.name,o.type,o.type_desc FROM sys.objects AS o JOIN sys.schemas AS s ON s.schema_id=o.schema_id WHERE o.is_ms_shipped=0 ORDER BY s.name,o.name;`r`nGO`r`n" +
            "SELECT s.name AS schema_name,o.name,o.type,m.definition FROM sys.objects AS o JOIN sys.schemas AS s ON s.schema_id=o.schema_id JOIN sys.sql_modules AS m ON m.object_id=o.object_id WHERE o.is_ms_shipped=0 ORDER BY s.name,o.name;`r`nGO`r`n"
        if((Test-Path -LiteralPath $catalogPath -PathType Leaf) -and -not ((Get-Content -LiteralPath $catalogPath -TotalCount 1) -like '-- Generated from the rebuild manifest*')) { throw "Existing handwritten file was protected: $catalogPath" }
        [IO.File]::WriteAllText($catalogPath,$catalogSql,(New-Object Text.UTF8Encoding($false)))
        Add-ProjectFile $document $queryItems ($databaseName+' Catalog.sql') ($ProjectName+'\'+$catalogRelative) $Server $UserName $WindowsAuthentication
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
                        throw "Existing handwritten file was protected: $outputPath"
                    }
                    [System.IO.File]::WriteAllText($outputPath,$sql,(New-Object System.Text.UTF8Encoding($false)))
                    $relativePath=$relative.Replace('/','\')
                    Add-ProjectFile $document $queryItems ((Split-Path -Leaf $relativePath)) ($ProjectName+'\'+$relativePath) $Server $UserName $WindowsAuthentication
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


$script:OwnerValue='Seamless.SsmsWorkspace.v1'
$script:OwnerFile='.seamless-ssms-owner'
$script:LedgerFile='managed-ssms-files.json'
$script:PublishRoot=[IO.Path]::GetFullPath($PublishDirectory)
function Assert-NoReparse([string]$Path) {
    $current=[IO.Path]::GetFullPath($Path)
    while($current) {
        if(Test-Path -LiteralPath $current) {
            if((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point is not allowed: $current" }
        }
        $parent=[IO.Path]::GetDirectoryName($current)
        if($parent -eq $current) { break }
        $current=$parent
    }
}
function Resolve-OwnedPath([string]$Root,[string]$Relative) {
    if([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative)) { throw 'A managed SSMS path must be relative.' }
    $prefix=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
    $path=[IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if(-not $path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Managed SSMS path escapes its root.' }
    Assert-NoReparse $path
    return $path
}
function Get-RelativePath([string]$Root,[string]$Path) {
    $rootPath=[IO.Path]::GetFullPath($Root);$targetPath=[IO.Path]::GetFullPath($Path)
    if([IO.Path]::GetPathRoot($rootPath) -ine [IO.Path]::GetPathRoot($targetPath)) { return $targetPath }
    $base=New-Object Uri ($rootPath.TrimEnd('\')+'\')
    $target=New-Object Uri ([IO.Path]::GetFullPath($Path))
    return [Uri]::UnescapeDataString($base.MakeRelativeUri($target).ToString()).Replace('/','\')
}
function Get-Sha256([string]$Path) {
    $algorithm=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','') } finally { $stream.Dispose();$algorithm.Dispose() }
}
function Write-JsonFile([string]$Path,$Value) {
    [IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 20),(New-Object Text.UTF8Encoding($false)))
}
function Assert-ManagedRelative([string]$Relative) {
    if($Relative -notmatch '^(SQLServer|SeamlessDB)\\(Generated\\[^\\]+\\(?:Catalog\.sql|[^\\]+\\(?:Table|View)-[^\\]+\.sql)|(?:SQLServer|SeamlessDB)\.ssmssqlproj)$') { throw "Invalid managed SSMS file: $Relative" }
    if($Relative -like '*.ssmssqlproj' -and $Relative.Split('\')[0]+'.ssmssqlproj' -cne $Relative.Split('\')[1]) { throw 'Project file does not match its directory.' }
}
function Read-Ledger([string]$Root,[bool]$Required=$true) {
    Assert-NoReparse $Root
    $owner=Resolve-OwnedPath $Root $script:OwnerFile
    $path=Resolve-OwnedPath $Root $script:LedgerFile
    if(-not (Test-Path -LiteralPath $path)) {
        if($Required -or (Test-Path -LiteralPath $owner)) { throw 'SSMS owner and ledger must both exist.' }
        return $null
    }
    if(-not (Test-Path -LiteralPath $owner) -or [IO.File]::ReadAllText($owner) -cne $script:OwnerValue) { throw 'SSMS owner does not match.' }
    $ledger=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
    if($ledger.Owner -cne $script:OwnerValue) { throw 'SSMS ledger owner does not match.' }
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in @($ledger.Files)) {
        Assert-ManagedRelative $entry.Path
        if(-not $seen.Add($entry.Path)) { throw 'Duplicate SSMS ledger path.' }
        $file=Resolve-OwnedPath $Root $entry.Path
        if(-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Sha256 $file) -cne $entry.Sha256) { throw "Managed SSMS file was changed or removed: $file" }
    }
    return $ledger
}
function Validate-Stage([string]$Root) {
    $ledger=Read-Ledger $Root
    $managed=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in @($ledger.Files)) { [void]$managed.Add($entry.Path) }
    foreach($side in @('SQLServer','SeamlessDB')) {
        $relative=$side+'\'+$side+'.ssmssqlproj'
        if(-not $managed.Contains($relative)) { throw 'SSMS project is absent from the stage ledger.' }
        [xml]$document=[IO.File]::ReadAllText((Resolve-OwnedPath $Root $relative))
        if($document.DocumentElement.LocalName -cne 'SqlWorkbenchSqlProject' -or $document.DocumentElement.GetAttribute('Name') -cne $side) { throw 'Invalid SSMS project XML identity.' }
        $hasAdventureWorks=@($document.SelectNodes('//ConnectionNode/InitialDB') | Where-Object { $_.InnerText -ceq 'AdventureWorks2016' }).Count -gt 0
        $basicReference=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Cases\AdventureWorks\Basic.sql'))
        $generatedReference=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Cases\AdventureWorks\GeneratedMethods.sql'))
        $foundBasic=$false;$foundGenerated=$false
        foreach($node in $document.SelectNodes('//FileNode/FullPath')) {
            $value=[string]$node.InnerText
            if($value.StartsWith($side+'\Generated\',[StringComparison]::OrdinalIgnoreCase)) {
                if(-not $managed.Contains($value)) { throw "Project references an unmanaged generated file: $value" }
                [void](Resolve-OwnedPath $Root $value)
            } else {
                if([IO.Path]::IsPathRooted($value)) { $resolved=[IO.Path]::GetFullPath($value) }
                else { $resolved=[IO.Path]::GetFullPath((Join-Path $ledger.TargetRoot $value)) }
                $allowed=@(@('Read-AggregateJoinView.sql','Fixture-CRUD-CommitRollback.sql')|ForEach-Object {[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\Cases\Common\'+$_)))})
                if($hasAdventureWorks) { $allowed+= $basicReference; if($side -ceq 'SeamlessDB') { $allowed+= $generatedReference } }
                if($allowed -cnotcontains $resolved -or -not (Test-Path -LiteralPath $resolved -PathType Leaf)) { throw "Invalid or missing handwritten SQL reference: $value" }
                if($resolved -ceq $basicReference) { $foundBasic=$true }
                if($resolved -ceq $generatedReference) { $foundGenerated=$true }
                Assert-NoReparse $resolved
            }
        }
        if($hasAdventureWorks -and -not $foundBasic) { throw 'AdventureWorks Basic.sql reference is missing.' }
        if($hasAdventureWorks -and $side -ceq 'SeamlessDB' -and -not $foundGenerated) { throw 'AdventureWorks GeneratedMethods.sql reference is missing.' }
        if($document.SelectNodes('//*[contains(translate(name(),"PASSWORD","password"),"password")]').Count -ne 0) { throw 'SSMS projects must not contain password elements.' }
    }
    return $ledger
}
function Build-Stage([string]$Root) {
    if([string]::IsNullOrWhiteSpace($script:WindowsAuthenticationName)) {
        if($PSUICulture -like 'ja*') { $script:WindowsAuthenticationName='Windows 認証' }
        elseif($PSUICulture -like 'en*') { $script:WindowsAuthenticationName='Windows Authentication' }
        else { throw 'Specify -WindowsAuthenticationName using the Windows authentication name saved by the target SSMS UI language.' }
    }
    if(-not $GenerationManifest -or -not (Test-Path -LiteralPath $GenerationManifest -PathType Leaf)) { throw 'A generation manifest is required.' }
    Assert-NoReparse $Root
    if((Test-Path -LiteralPath $Root) -and @(Get-ChildItem -LiteralPath $Root -Force).Count -ne 0) { throw 'SSMS staging directory must be new and empty.' }
    $manifest=Get-Content -LiteralPath $GenerationManifest -Raw -Encoding UTF8|ConvertFrom-Json
    $databases=@($manifest.Databases)
    if($databases.Count -eq 0) { throw 'No databases exist in the generation.' }
    foreach($database in $databases) { if([string]::IsNullOrWhiteSpace([string]$database.Name) -or $null -eq $database.Catalog -or $null -eq $database.Catalog.Schemas) { throw 'Generation database catalog is incomplete.' } }
    if(-not $manifest.PSObject.Properties['SourceServer'] -or -not $manifest.PSObject.Properties['SourceIntegratedSecurity']) { throw 'Generation source connection metadata is missing; rebuild the manifest.' }
    $server=[string]$manifest.SourceServer
    if($server -notin @('localhost','tcp:localhost') -or $manifest.SourceIntegratedSecurity -isnot [bool] -or -not $manifest.SourceIntegratedSecurity) { throw 'The source metadata must specify localhost with Windows integrated security.' }
    [void](New-Item -ItemType Directory -Force -Path $Root)
    foreach($side in @('SQLServer','SeamlessDB')) { [void](New-Item -ItemType Directory -Force -Path (Join-Path $Root $side)) }
    New-SqlProject (Join-Path $Root 'SQLServer') 'SQLServer' $databases $server $true '' 'SQLServer'
    New-SqlProject (Join-Path $Root 'SeamlessDB') 'SeamlessDB' $databases '127.0.0.1,14333' $false 'sa' 'SeamlessDB'
    $files=@(Get-ChildItem -LiteralPath $Root -File -Recurse|ForEach-Object { @{Path=(Get-RelativePath $Root $_.FullName);Sha256=(Get-Sha256 $_.FullName)} })
    [IO.File]::WriteAllText((Join-Path $Root $script:OwnerFile),$script:OwnerValue)
    Write-JsonFile (Join-Path $Root $script:LedgerFile) @{Owner=$script:OwnerValue;Generation=$manifest.Id;TargetRoot=$script:PublishRoot;Files=$files}
    [void](Validate-Stage $Root)
}
function Publish-Stage([string]$Stage,[string]$Target) {
    $ledger=Validate-Stage $Stage
    if([IO.Path]::GetFullPath($ledger.TargetRoot) -cne $Target) { throw 'Stage publication target does not match its ledger.' }
    Assert-NoReparse $Target
    [void](New-Item -ItemType Directory -Force -Path $Target)
    $publicationLock=New-Object IO.FileStream((Resolve-OwnedPath $Target '.ssms-publish.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try {
    $old=Read-Ledger $Target $false
    if($old -and [IO.Path]::GetFullPath($old.TargetRoot) -cne $Target) { throw 'Existing SSMS ledger belongs to another target.' }
    $oldFiles=@{};if($old) { foreach($entry in @($old.Files)) { $oldFiles[$entry.Path]=$entry } }
    $newFiles=@{};foreach($entry in @($ledger.Files)) { $newFiles[$entry.Path]=$entry }
    # Validate every overwrite and deletion before changing the destination.
    foreach($entry in @($ledger.Files)) {
        $targetFile=Resolve-OwnedPath $Target $entry.Path
        if((Test-Path -LiteralPath $targetFile) -and -not $oldFiles.ContainsKey($entry.Path)) {
            if($entry.Path -like '*.sql') {
                if((Get-Sha256 $targetFile) -cne $entry.Sha256) { throw "Existing unowned SQL is protected: $targetFile" }
            } else {
                [xml]$existing=[IO.File]::ReadAllText($targetFile)
                if($existing.DocumentElement.LocalName -cne 'SqlWorkbenchSqlProject' -or $existing.DocumentElement.GetAttribute('Name') -cne $entry.Path.Split('\')[0]) { throw 'Existing project does not have the expected generated identity.' }
            }
        }
    }
    $changes=@($ledger.Files|ForEach-Object {$_.Path})+@($oldFiles.Keys|Where-Object {-not $newFiles.ContainsKey($_)})+@($script:OwnerFile,$script:LedgerFile)
    $backup=Join-Path $Stage ('publication-backup-'+[Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $backup)
    $before=@{}
    foreach($relative in $changes) {
        $path=Resolve-OwnedPath $Target $relative
        if(Test-Path -LiteralPath $path) { $saved=Resolve-OwnedPath $backup $relative;[void](New-Item -ItemType Directory -Force -Path (Split-Path -Parent $saved));[IO.File]::Copy($path,$saved);$before[$relative]=$saved } else { $before[$relative]=$null }
    }
    try {
        foreach($entry in @($ledger.Files)) { $path=Resolve-OwnedPath $Target $entry.Path;[void](New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path));[IO.File]::Copy((Resolve-OwnedPath $Stage $entry.Path),$path,$true) }
        foreach($relative in @($oldFiles.Keys|Where-Object {-not $newFiles.ContainsKey($_)})) { Remove-Item -LiteralPath (Resolve-OwnedPath $Target $relative) -Force }
        [IO.File]::WriteAllText((Join-Path $Target $script:OwnerFile),$script:OwnerValue)
        Write-JsonFile (Join-Path $Target $script:LedgerFile) $ledger
        [void](Validate-Stage $Target)
        foreach($relative in @($oldFiles.Keys|Where-Object {-not $newFiles.ContainsKey($_) -and $_ -like '*.sql'})) {
            $parent=Split-Path -Parent (Resolve-OwnedPath $Target $relative)
            $limit=Join-Path $Target ($relative.Split('\')[0]+'\Generated')
            while($parent -and $parent -cne $limit) {
                Assert-NoReparse $parent
                if(-not (Test-Path -LiteralPath $parent) -or @(Get-ChildItem -LiteralPath $parent -Force).Count -ne 0) { break }
                Remove-Item -LiteralPath $parent -Force
                $parent=Split-Path -Parent $parent
            }
        }
    } catch {
        $failure=$_
        foreach($relative in $changes) { $path=Resolve-OwnedPath $Target $relative;if($before[$relative]) { [void](New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path));[IO.File]::Copy($before[$relative],$path,$true) } elseif(Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force } }
        throw $failure
    }
    } finally { $publicationLock.Dispose() }
}
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
if($Operation -eq 'Stage') { Build-Stage $outputRoot;Write-Output "SSMS stage validated: $outputRoot" }
elseif($Operation -eq 'Publish') { if(-not $StagingDirectory) { throw 'StagingDirectory is required for Publish.' };Publish-Stage ([IO.Path]::GetFullPath($StagingDirectory)) $outputRoot;Write-Output "SSMS stage published: $outputRoot" }
elseif($Operation -eq 'Validate') { [void](Validate-Stage $outputRoot);Write-Output "SSMS workspace validated: $outputRoot" }
else { $script:PublishRoot=$outputRoot;$stage=Join-Path ([IO.Path]::GetDirectoryName($outputRoot)) ('ssms-stage-'+[Guid]::NewGuid().ToString('N'));Build-Stage $stage;Publish-Stage $stage $outputRoot;Write-Output "SSMS workspace updated: $outputRoot" }
