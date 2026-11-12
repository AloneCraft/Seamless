$path = "Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$contentLF = $content -replace "\r\n", "`n"

$p1 = "    private bool CanUseDirectSpanPipelineOrder(out int orderStageIndex){"
$p2 = "    private string? RenderDirectSpanPipelineOrderBy(string actualVariableName,string actualTypeName){"
Write-Output "Index p1: $($contentLF.IndexOf($p1))"
Write-Output "Index p2: $($contentLF.IndexOf($p2))"

$sub = $contentLF.Substring($contentLF.IndexOf($p1), $contentLF.IndexOf($p2) - $contentLF.IndexOf($p1) + 120)
Write-Output "--- Substring Preview ---"
Write-Output $sub
