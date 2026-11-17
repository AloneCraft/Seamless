$lines = [System.IO.File]::ReadAllLines('Seamless/FLinq/Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs', [System.Text.Encoding]::UTF8)
for ($i=3471; $i -le 3536; $i++) {
    Write-Output $lines[$i]
}
