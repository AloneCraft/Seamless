$path = "Seamless/FLinq/Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

$target = @"
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.DefaultIfEmpty:
                    // DefaultIfEmpty は要素を変えない(空のときの既定値は呼び出し側が先に用意する)。
                    break;
"@

# Check with whatever line endings $content has
$targetLF = $target -replace "\r\n", "`n"
$targetCRLF = $target -replace "\r\n", "`n" -replace "`n", "`r`n"

$replacement = @"
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.DefaultIfEmpty:
                case PipelineStageKind.Take:
                    // DefaultIfEmpty は要素を変えない(空のときの既定値は呼び出し側が先に用意する)。Take は Top-K で既に件数制限済み。
                    break;
"@

if ($content.Contains($targetCRLF)) {
    $content = $content.Replace($targetCRLF, ($replacement -replace "\r\n", "`n" -replace "`n", "`r`n"))
    [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
    Write-Output "Replaced with CRLF"
} elseif ($content.Contains($targetLF)) {
    $content = $content.Replace($targetLF, ($replacement -replace "\r\n", "`n"))
    [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
    Write-Output "Replaced with LF"
} else {
    throw "Target not found in either CRLF or LF!"
}
