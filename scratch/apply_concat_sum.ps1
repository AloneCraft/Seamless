$path = "Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineTerminal.cs"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

$target = "            if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {"
$idx = $content.IndexOf($target)
if ($idx -lt 0) {
    throw "Target not found: $target"
}

$insertCode = @'
            if(terminalKind==PipelineTerminalKind.Sum && binaryStage.Kind==PipelineStageKind.Concat) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
                this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
                this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
                builder.AppendLine($"    for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                builder.AppendLine($"        var __item{this.result}={firstSpanName}[__i{this.result}];");
                var filteredFirst = this.AppendBinaryBeforeStages(builder,$"__item{this.result}","        ",binaryStageIndex);
                var hitFirst = this.AppendBinaryAfterStages(builder,filteredFirst,"        ",binaryStageIndex);
                builder.AppendLine($"        {actualVariableName}+={hitFirst};");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                builder.AppendLine($"using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"    var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"    for(var __j{this.result}=0;__j{this.result}<{secondSpanName}.Length;__j{this.result}++) {{");
                builder.AppendLine($"        var __item2{this.result}={secondSpanName}[__j{this.result}];");
                var hitSecond = this.AppendBinaryAfterStages(builder,$"__item2{this.result}","        ",binaryStageIndex);
                builder.AppendLine($"        {actualVariableName}+={hitSecond};");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }

'@

$prefix = $content.Substring(0, $idx)
$suffix = $content.Substring($idx)
$finalContent = $prefix + $insertCode + $suffix

[System.IO.File]::WriteAllText($path, $finalContent, [System.Text.Encoding]::UTF8)
Write-Output "Successfully inserted Concat+Sum direct execution"
