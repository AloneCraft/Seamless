$path = "Seamless/FLinq/Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

$oldPattern = @"
    private bool CanUseDirectSpanPipelineOrder(out int orderStageIndex){
        orderStageIndex=-1;
        if(terminalKind is not (PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList)){
            return false;
        }
        var count=0;
        for(var i=0;i<pipeline.Stages.Count;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending){
                if(stage.UsesIndex){
                    return false;
                }
                if(stage.Func is { } func&&!CanUseDirectSpanPipelineFunc(func)){
                    return false;
                }
                orderStageIndex=i;
                count++;
            }
        }
        if(count!=1){
            orderStageIndex=-1;
            return false;
        }
        for(var i=0;i<orderStageIndex;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is not (PipelineStageKind.AsEnumerable or PipelineStageKind.Cast)){
                orderStageIndex=-1;
                return false;
            }
        }
        for(var i=orderStageIndex+1;i<pipeline.Stages.Count;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable)){
                orderStageIndex=-1;
                return false;
            }
            if(stage.Kind==PipelineStageKind.Select){
                if(stage.UsesIndex||stage.Func is not { } selectFunc||!CanUseDirectSpanPipelineFunc(selectFunc)){
                    orderStageIndex=-1;
                    return false;
                }
            }
        }
        return true;
    }
"@

$found = $content.Contains($oldPattern)
Write-Output "Found with CRLF: $found"
if (-not $found) {
    # Try normalizing line endings to find
    $cNormalized = $content -replace "\r\n", "`n"
    $pNormalized = $oldPattern -replace "\r\n", "`n"
    Write-Output "Found with LF: $($cNormalized.Contains($pNormalized))"
}
