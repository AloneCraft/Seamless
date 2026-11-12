$path = "Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$contentLF = $content -replace "\r\n", "`n"

$oldCode = @'
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.DefaultIfEmpty:
                    // DefaultIfEmpty は要素を変えない(空のときの既定値は呼び出し側が先に用意する)。
                    break;
'@ -replace "\r\n", "`n"

$newCode = @'
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.DefaultIfEmpty:
                case PipelineStageKind.Take:
                    // DefaultIfEmpty は要素を変えない(空のときの既定値は呼び出し側が先に用意する)。Take は Top-K で既に件数制限済み。
                    break;
'@ -replace "\r\n", "`n"

if (-not $contentLF.Contains($oldCode)) {
    throw "oldCode not found in PipelineTerminal.cs!"
}

$replaced = $contentLF.Replace($oldCode, $newCode)
[System.IO.File]::WriteAllText($path, $replaced, [System.Text.Encoding]::UTF8)
Write-Output "Successfully updated AppendDirectSpanPipelineStages!"
