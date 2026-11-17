$path = "Seamless/FLinq/Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$target = "case PipelineStageKind.DefaultIfEmpty:"
$idx = $content.IndexOf($target)
Write-Output "Index: $idx"

$sub = $content.Substring($idx - 50, 150)
Write-Output "Substring: [$sub]"
