$path = 'Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs'
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$lines = $content.Split([char]10)
for ($i=0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^\s*private (bool|string\?).*(CanUseDirectSpanPipelineOrder|RenderDirectSpanPipelineOrder)') {
        Write-Output ("Line " + $i + ": " + $lines[$i].Trim())
    }
}
