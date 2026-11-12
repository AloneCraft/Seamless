$path = "Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$contentLF = $content -replace "\r\n", "`n"

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

$canUseNew = @'
    private bool CanUseDirectSpanPipelineOrder(out int orderStageIndex){
        return this.CanUseDirectSpanPipelineOrder(out orderStageIndex, out _, out _, out _);
    }

    private bool CanUseDirectSpanPipelineOrder(out int orderStageIndex, out List<PipelineStage> orderStages, out int takeStageIndex, out string? takeCountExpr){
        orderStageIndex=-1;
        orderStages=new List<PipelineStage>();
        takeStageIndex=-1;
        takeCountExpr=null;
        if(terminalKind is not (PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList)){
            return false;
        }
        for(var i=0;i<pipeline.Stages.Count;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending){
                orderStageIndex=i;
                break;
            }
        }
        if(orderStageIndex<0){
            return false;
        }
        for(var i=0;i<orderStageIndex;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is not (PipelineStageKind.AsEnumerable or PipelineStageKind.Cast)){
                return false;
            }
        }
        for(var i=orderStageIndex;i<pipeline.Stages.Count;i++){
            var stage=pipeline.Stages[i];
            if(i==orderStageIndex){
                if(stage.UsesIndex||(stage.Func is { } func&&!CanUseDirectSpanPipelineFunc(func))){
                    return false;
                }
                orderStages.Add(stage);
            }else if(stage.Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending){
                if(stage.UsesIndex||(stage.Func is { } func&&!CanUseDirectSpanPipelineFunc(func))){
                    return false;
                }
                orderStages.Add(stage);
            }else{
                break;
            }
        }
        if(orderStages.Count < 1 || orderStages.Count > 2){
            return false;
        }
        var afterOrderIndex = orderStageIndex + orderStages.Count;
        for(var i=afterOrderIndex;i<pipeline.Stages.Count;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind==PipelineStageKind.Take){
                if(takeStageIndex>=0||stage.UsesIndex||stage.Value is null){
                    return false;
                }
                takeStageIndex=i;
                takeCountExpr=stage.Value;
            }else if(stage.Kind is PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable){
                if(stage.Kind==PipelineStageKind.Select){
                    if(stage.UsesIndex||stage.Func is not { } selectFunc||!CanUseDirectSpanPipelineFunc(selectFunc)){
                        return false;
                    }
                }
            }else{
                return false;
            }
        }
        return true;
    }

    private string? RenderDirectSpanPipelineOrderTopK(string actualVariableName, string actualTypeName, int orderStageIndex, List<PipelineStage> orderStages, int takeStageIndex, string takeCountExpr) {
        var sourceName = $"__pipelineSrc{this.result}";
        string sourceExpression;
        string directSpanExpression;
        try {
            sourceExpression = pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        directSpanExpression = pipeline.Source.GetDirectSpanExpression(sourceName) ?? string.Empty;
        if(directSpanExpression.Length == 0) {
            return null;
        }

        var primaryOrder = orderStages[0];
        var isDescending1 = primaryOrder.Kind == PipelineStageKind.OrderByDescending;
        var keyType1 = primaryOrder.KeyType ?? primaryOrder.Func?.ReturnTypeName ?? pipeline.ElementType;

        var hasSecondary = orderStages.Count > 1;
        var secondaryOrder = hasSecondary ? orderStages[1] : null;
        var isDescending2 = secondaryOrder?.Kind == PipelineStageKind.ThenByDescending;
        var keyType2 = secondaryOrder is not null ? (secondaryOrder.KeyType ?? secondaryOrder.Func?.ReturnTypeName ?? pipeline.ElementType) : "int";

        var spanName = $"__pipelineSpan{this.result}";
        var iName = $"__pipelineI{this.result}";
        var kName = $"__topK{this.result}";
        var countName = $"__topKCount{this.result}";
        var indicesName = $"__topIndices{this.result}";
        var pKeysName = $"__topPKeys{this.result}";
        var sKeysName = $"__topSKeys{this.result}";
        var posName = $"__pos{this.result}";
        var itemVar = $"__item{this.result}";
        var pkeyVar = $"__pkey{this.result}";
        var skeyVar = $"__skey{this.result}";

        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    var __takeCap{this.result}={takeCountExpr};");
        builder.AppendLine($"    if({spanName}.Length==0 || __takeCap{this.result}<=0) {{");
        if(terminalKind == PipelineTerminalKind.ToArray) {
            builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
        } else {
            builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
        }
        builder.AppendLine("    } else {");
        builder.AppendLine($"        var {kName}=global::System.Math.Min(__takeCap{this.result},{spanName}.Length);");
        builder.AppendLine($"        global::System.Span<int> {indicesName}={kName}<=64 ? stackalloc int[64] : new int[{kName}];");
        builder.AppendLine($"        global::System.Span<{keyType1}> {pKeysName}={kName}<=64 ? stackalloc {keyType1}[64] : new {keyType1}[{kName}];");
        if(hasSecondary) {
            builder.AppendLine($"        global::System.Span<{keyType2}> {sKeysName}={kName}<=64 ? stackalloc {keyType2}[64] : new {keyType2}[{kName}];");
        }
        builder.AppendLine($"        var {countName}=0;");
        builder.AppendLine($"        for(var {iName}=0;{iName}<{spanName}.Length;{iName}++) {{");
        builder.AppendLine($"            ref readonly var {itemVar}=ref {spanName}[{iName}];");
        builder.AppendLine($"            {keyType1} {pkeyVar}=default!;");
        if(primaryOrder.Func is null) {
            builder.AppendLine($"            {pkeyVar}={itemVar};");
        } else {
            builder.AppendLine(IndentLines(this.FuncAssignStatic(primaryOrder.Func,"OrderBy",pkeyVar,itemVar),"            "));
        }
        if(hasSecondary) {
            builder.AppendLine($"            {keyType2} {skeyVar}=default!;");
            if(secondaryOrder!.Func is null) {
                builder.AppendLine($"            {skeyVar}={itemVar};");
            } else {
                builder.AppendLine(IndentLines(this.FuncAssignStatic(secondaryOrder.Func,"ThenBy",skeyVar,itemVar),"            "));
            }
        }

        string CompExpr(string curPk, string curSk, string targetPk, string targetSk, string targetIdx) {
            var cmpP = isDescending1 ? $"{targetPk}.CompareTo({curPk})" : $"{curPk}.CompareTo({targetPk})";
            if(!hasSecondary) {
                return $"({cmpP}<0 || ({cmpP}==0 && {iName}<{targetIdx}))";
            }
            var cmpS = isDescending2 ? $"{targetSk}.CompareTo({curSk})" : $"{curSk}.CompareTo({targetSk})";
            return $"({cmpP}<0 || ({cmpP}==0 && ({cmpS}<0 || ({cmpS}==0 && {iName}<{targetIdx}))))";
        }

        string IsWorseOrEqualExpr(string curPk, string curSk, string targetPk, string targetSk, string targetIdx) {
            var cmpP = isDescending1 ? $"{targetPk}.CompareTo({curPk})" : $"{curPk}.CompareTo({targetPk})";
            if(!hasSecondary) {
                return $"({cmpP}>0 || ({cmpP}==0 && {iName}>={targetIdx}))";
            }
            var cmpS = isDescending2 ? $"{targetSk}.CompareTo({curSk})" : $"{curSk}.CompareTo({targetSk})";
            return $"({cmpP}>0 || ({cmpP}==0 && ({cmpS}>0 || ({cmpS}==0 && {iName}>={targetIdx}))))";
        }

        builder.AppendLine($"            if({countName}<{kName}) {{");
        builder.AppendLine($"                int {posName}={countName};");
        builder.AppendLine($"                while({posName}>0 && {CompExpr(pkeyVar, skeyVar, $"{pKeysName}[{posName}-1]", hasSecondary ? $"{sKeysName}[{posName}-1]" : "0", $"{indicesName}[{posName}-1]")}) {{");
        builder.AppendLine($"                    {indicesName}[{posName}]={indicesName}[{posName}-1];");
        builder.AppendLine($"                    {pKeysName}[{posName}]={pKeysName}[{posName}-1];");
        if(hasSecondary) {
            builder.AppendLine($"                    {sKeysName}[{posName}]={sKeysName}[{posName}-1];");
        }
        builder.AppendLine($"                    {posName}--;");
        builder.AppendLine("                }");
        builder.AppendLine($"                {indicesName}[{posName}]={iName};");
        builder.AppendLine($"                {pKeysName}[{posName}]={pkeyVar};");
        if(hasSecondary) {
            builder.AppendLine($"                {sKeysName}[{posName}]={skeyVar};");
        }
        builder.AppendLine($"                {countName}++;");
        builder.AppendLine("            } else {");
        builder.AppendLine($"                if({IsWorseOrEqualExpr(pkeyVar, skeyVar, $"{pKeysName}[{kName}-1]", hasSecondary ? $"{sKeysName}[{kName}-1]" : "0", $"{indicesName}[{kName}-1]")}) continue;");
        builder.AppendLine($"                int {posName}={kName}-1;");
        builder.AppendLine($"                while({posName}>0 && {CompExpr(pkeyVar, skeyVar, $"{pKeysName}[{posName}-1]", hasSecondary ? $"{sKeysName}[{posName}-1]" : "0", $"{indicesName}[{posName}-1]")}) {{");
        builder.AppendLine($"                    {indicesName}[{posName}]={indicesName}[{posName}-1];");
        builder.AppendLine($"                    {pKeysName}[{posName}]={pKeysName}[{posName}-1];");
        if(hasSecondary) {
            builder.AppendLine($"                    {sKeysName}[{posName}]={sKeysName}[{posName}-1];");
        }
        builder.AppendLine($"                    {posName}--;");
        builder.AppendLine("                }");
        builder.AppendLine($"                {indicesName}[{posName}]={iName};");
        builder.AppendLine($"                {pKeysName}[{posName}]={pkeyVar};");
        if(hasSecondary) {
            builder.AppendLine($"                {sKeysName}[{posName}]={skeyVar};");
        }
        builder.AppendLine("            }");
        builder.AppendLine("        }");

        var afterOrderIndex = orderStageIndex + orderStages.Count;
        if(terminalKind == PipelineTerminalKind.ToArray) {
            builder.AppendLine($"        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({kName});");
            builder.AppendLine($"        for(var {iName}=0;{iName}<{kName};{iName}++) {{");
            builder.AppendLine($"            var __sortedIdx{this.result}={indicesName}[{iName}];");
            builder.AppendLine($"            var __itemOut{this.result}={spanName}[__sortedIdx{this.result}];");
            var afterName = this.AppendDirectSpanPipelineStages(builder, $"__itemOut{this.result}", "            ", afterOrderIndex);
            builder.AppendLine($"            {actualVariableName}[{iName}]={afterName};");
            builder.AppendLine("        }");
        } else {
            builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({kName});");
            builder.AppendLine($"        for(var {iName}=0;{iName}<{kName};{iName}++) {{");
            builder.AppendLine($"            var __sortedIdx{this.result}={indicesName}[{iName}];");
            builder.AppendLine($"            var __itemOut{this.result}={spanName}[__sortedIdx{this.result}];");
            var afterName = this.AppendDirectSpanPipelineStages(builder, $"__itemOut{this.result}", "            ", afterOrderIndex);
            builder.AppendLine($"            {actualVariableName}.Add({afterName});");
            builder.AppendLine("        }");
        }
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }
'@ -replace "\r\n", "`n"

$renderOld = @'
    private string? RenderDirectSpanPipelineOrderBy(string actualVariableName,string actualTypeName){
        if(!this.CanUseDirectSpanPipelineOrder(out var orderStageIndex)){
            return null;
        }
'@ -replace "\r\n", "`n"

$renderNew = @'
    private string? RenderDirectSpanPipelineOrderBy(string actualVariableName,string actualTypeName){
        if(!this.CanUseDirectSpanPipelineOrder(out var orderStageIndex, out var orderStages, out var takeStageIndex, out var takeCountExpr)){
            return null;
        }
        if(takeCountExpr is not null){
            return this.RenderDirectSpanPipelineOrderTopK(actualVariableName, actualTypeName, orderStageIndex, orderStages, takeStageIndex, takeCountExpr);
        }
        if(orderStages.Count != 1){
            return null;
        }
'@ -replace "\r\n", "`n"

$c1 = $contentLF.Replace($canUseOld, $canUseNew)
$c2 = $c1.Replace($renderOld, $renderNew)

[System.IO.File]::WriteAllText($path, $c2, [System.Text.Encoding]::UTF8)
Write-Output "Successfully updated PipelineTerminal.cs!"
