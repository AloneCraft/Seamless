$path = "Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'AppendDirectSpanPipelineStages') {
        Write-Output "Found AppendDirectSpanPipelineStages at line $($i+1)"
        for ($j = $i; $j -lt $i + 20; $j++) {
            if ($lines[$j] -match 'case PipelineStageKind\.DefaultIfEmpty:') {
                Write-Output "Found DefaultIfEmpty at line $($j+1)"
                $lines[$j] = "                case PipelineStageKind.DefaultIfEmpty:`n                case PipelineStageKind.Take:"
                break
            }
        }
        break
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Output "Done line replacement!"
