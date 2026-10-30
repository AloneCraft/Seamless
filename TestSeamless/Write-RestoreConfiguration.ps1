param([Parameter(Mandatory)][string[]]$Sources,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
$Output=[IO.Path]::GetFullPath($Output)
$document=New-Object Xml.XmlDocument;$configuration=$document.CreateElement('configuration');[void]$document.AppendChild($configuration)
$sourcesElement=$document.CreateElement('packageSources');[void]$configuration.AppendChild($sourcesElement);[void]$sourcesElement.AppendChild($document.CreateElement('clear'))
$index=0
foreach($source in $Sources){
    if([string]::IsNullOrWhiteSpace($source)){throw 'A package source is empty.'}
    $uri=$null;$remote=[Uri]::TryCreate($source,[UriKind]::Absolute,[ref]$uri) -and $uri.Scheme -eq 'https'
    if(-not $remote -and [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($source)) -eq 'C:\'){throw 'Comparison restore sources must not use the C drive.'}
    $entry=$document.CreateElement('add');$entry.SetAttribute('key',('comparison-'+$index));$entry.SetAttribute('value',$source);[void]$sourcesElement.AppendChild($entry);$index++
}
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($Output))|Out-Null;$document.Save($Output)
