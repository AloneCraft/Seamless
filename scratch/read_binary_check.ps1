$lines = [System.IO.File]::ReadAllLines('Seamless/FLinq/Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs', [System.Text.Encoding]::UTF8)
$start = -1
for ($i=0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'CanUseDirectSpanBinaryShortCircuit\(') { $start = $i; break; }
}
for ($i=$start + 40; $i -le $start + 100; $i++) {
    Write-Output $lines[$i]
}
