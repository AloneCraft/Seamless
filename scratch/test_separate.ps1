$path = "Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$contentLF = $content -replace "\r\n", "`n"

$targetStart = "    private bool CanUseDirectSpanPipelineOrder(out int orderStageIndex){`n        orderStageIndex=-1;"
$idx1 = $contentLF.IndexOf($targetStart)

$targetEnd = "    private string? RenderDirectSpanPipelineOrderBy(string actualVariableName,string actualTypeName){`n        if(!this.CanUseDirectSpanPipelineOrder(out var orderStageIndex)){`n            return null;`n        }"
$idx2 = $contentLF.IndexOf($targetEnd)

if ($idx1 -lt 0 -or $idx2 -lt 0) {
    throw "Indices not found: idx1=$idx1, idx2=$idx2"
}

# The block to replace is:
# From $idx1 up to the end of CanUseDirectSpanPipelineOrder, PLUS the start of RenderDirectSpanPipelineOrderBy
# Actually, let's replace CanUseDirectSpanPipelineOrder separately, and RenderDirectSpanPipelineOrderBy start separately!

$canUseOld = @'
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
'@ -replace "\r\n", "`n"

$renderOld = @'
    private string? RenderDirectSpanPipelineOrderBy(string actualVariableName,string actualTypeName){
        if(!this.CanUseDirectSpanPipelineOrder(out var orderStageIndex)){
            return null;
        }
'@ -replace "\r\n", "`n"

Write-Output "canUseOld found: $($contentLF.Contains($canUseOld))"
Write-Output "renderOld found: $($contentLF.Contains($renderOld))"
