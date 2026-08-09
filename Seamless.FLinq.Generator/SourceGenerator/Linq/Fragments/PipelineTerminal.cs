namespace Seamless.FLinq;

/*
PipelineTerminal は PipelineEnumerable の終端を scalar/direct execution として出す。

入力例:
  source.Where(p).Select(s).ToArray()
  source.Where(p).Any()

生成例:
  using var e = pipeline.GetEnumerator();
  if(kind == Any) result = e.TryGetNext(out _);
  if(kind == Count) while(e.TryGetNext(out _)) count++;
  if(kind == ToArray) TryGetNonEnumeratedCount/TryCopyTo を試し、だめなら ArrayPool で蓄積する。

バリエーション:
  - Any/Count/First は単一結果 enumerator も direct execution も生成できる。
  - ToList/ToArray/ToDictionary/Sum は direct execution 専用で、GetEnumeratorExpression は使わない。
  - ToDictionary は func1 を keySelector、func2 を elementSelector として扱い、comparerValue を constructor に渡す。
*/
internal sealed class PipelineTerminal(int 番号,PipelineEnumerable pipeline,PipelineTerminalKind terminalKind,FuncFragment? func1 = null,FuncFragment? func2 = null,string? comparerValue = null):Scalar(番号), DirectExecutionFragment {
    protected override Enumerable PrimarySource => pipeline;
    private string ResultType {
        get {
            if(terminalKind==PipelineTerminalKind.Any) {
                return "bool";
            }
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int";
            }
            if(terminalKind==PipelineTerminalKind.First) {
                return pipeline.ElementType;
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    public override string StructMethod => pipeline.StructMethod;
    public override string GetEnumeratorStruct {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum) {
                return pipeline.GetEnumeratorStruct;
            }
            return $$"""
                {{pipeline.GetEnumeratorStruct}}
                {{this.DefineScalarEnumeratorStruct(this.ResultType)}}
                """;
        }
    }
    public override string GetEnumeratorExpression {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum) {
                throw new NotSupportedException($"{nameof(PipelineTerminal)} uses direct execution.");
            }
            return $"new {this.EnumeratorTypeName}({pipeline.GetEnumeratorExpression})";
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorConstructor => $"this.Current = Compute({this.e});";
    public override string GetEnumeratorFields => $$"""
        public {{this.ResultType}} Current { get; private set; }
        private bool {{this.yielded}} = false;
        {{AggressiveInliningAttribute}}
        private static {{this.ResultType}} Compute(TEnum source){
            {{this.ComputeBody}}
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}){
            current=default!;
            return false;
        }
        this.{{this.yielded}} = true;
        current = this.Current;
        return true;
        """;
    protected override string GetCurrentPropertyDeclaration(string currentType) => string.Empty;
    private string ComputeBody {
        get {
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int count = 0;\nwhile(source.TryGetNext(out _)) count++;\nreturn count;";
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                return "return source.TryGetNext(out _);";
            }
            if(terminalKind==PipelineTerminalKind.First) {
                return "if(source.TryGetNext(out var item)) return item;\nthrow global::Seamless.Linq.GeneratedCodeHelpers.NoElements();";
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    private bool CanUseDirectSpanPipelineCopy() {
        foreach(var stage in pipeline.Stages) {
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.Cast:
                    break;
                case PipelineStageKind.Select:
                    if(stage.UsesIndex||stage.Func is not { } func||!CanUseDirectSpanPipelineFunc(func)) {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    private static bool CanUseDirectSpanPipelineFunc(FuncFragment func) {
        if(!func.CanInlineAssign
            ||func.CapturedLocals.Length!=0
            ||func.HoistedLocals.Length!=0
            ||func.RequiresStatementInlining
            ||func.ParameterNames.Length!=1
            ||string.IsNullOrWhiteSpace(func.ParameterNames[0])) {
            return false;
        }

        var parameterPattern = $@"\b{System.Text.RegularExpressions.Regex.Escape(func.ParameterNames[0])}\b";
        var body = System.Text.RegularExpressions.Regex.Replace(func.InlineBody,parameterPattern,"__p",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return !System.Text.RegularExpressions.Regex.IsMatch(body,@"\b(?!__p\b)[A-Za-z_@]\w*\b",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private string? TryGetDirectSourceSpanSetup(string sourceVar,out string sourceExpression,out string directSpanExpression) {
        sourceExpression=string.Empty;
        directSpanExpression=string.Empty;
        if(!this.CanUseDirectSpanPipelineCopy()) {
            return null;
        }
        try {
            sourceExpression=pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        directSpanExpression=pipeline.Source.GetDirectSpanExpression(sourceVar)??string.Empty;
        return directSpanExpression.Length==0 ? null : string.Empty;
    }

    private string AppendDirectSpanPipelineStages(StringBuilder builder,string currentName,string indent) {
        for(var index = 0;index<pipeline.Stages.Count;index++) {
            var stage = pipeline.Stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                    break;
                case PipelineStageKind.Cast: {
                    var nextName = $"__pipelineItem{this.result}_{index+1}";
                    builder.AppendLine($"{indent}var {nextName}=({stage.OutputType})(object?){currentName};");
                    currentName=nextName;
                    break;
                }
                case PipelineStageKind.Select: {
                    var nextName = $"__pipelineItem{this.result}_{index+1}";
                    builder.AppendLine($"{indent}{stage.OutputType} {nextName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                    currentName=nextName;
                    break;
                }
                default:
                    throw new NotSupportedException($"{stage.Kind} is not a direct span pipeline copy stage.");
            }
        }
        return currentName;
    }

    private string? RenderDirectSpanPipelineToList(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetup(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var destinationName = $"__pipelineDestination{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        // Direct span pipeline copy avoids runtime TryCopyTo for count-preserving chains.
        // Example: `int[] src; src.Select(static x=>x+1).ToArray()` emits
        // `var __pipelineSpan=__pipelineSrc.GetSpan();` and fills the destination by index.
        // IEnumerable<T>, Tsavorite, and Persisted roots have no direct span expression, so
        // they keep the runtime fallback or backend-native scan path.
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    {actualVariableName}=new({spanName}.Length);");
        builder.AppendLine($"    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{spanName}.Length);");
        builder.AppendLine($"    var {destinationName}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"        ");
        builder.AppendLine($"        {destinationName}[{indexName}]={currentName};");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToArray(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetup(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        // See RenderDirectSpanPipelineToList: direct span roots prove the count at generation
        // time, so Select/AsEnumerable/identity Cast materialization can skip TryCopyTo.
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    if({spanName}.Length==0) {{");
        builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
        builder.AppendLine("    } else {");
        builder.AppendLine($"        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({spanName}.Length);");
        builder.AppendLine($"        for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"            var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"            ");
        builder.AppendLine($"            {actualVariableName}[{indexName}]={currentName};");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        var builder = new StringBuilder();
        if(terminalKind==PipelineTerminalKind.ToList) {
            if(this.RenderDirectSpanPipelineToList(actualVariableName,actualTypeName) is { } directSpanPipeline) {
                return directSpanPipeline;
            }
            // using ではなく try-finally なのは、遅経路のヘルパーへ enumerator を ref で渡すため。
            // using ローカルは読み取り専用で ref に渡せず、値渡しにするとコンストラクタで
            // プール配列を借りる enumerator（OrderBy 等）で二重 Return になる。
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine("try {");
            builder.AppendLine($"    if({this.EnumeratorName()}.TryGetNonEnumeratedCount(out var __count{this.result})) {{");
            builder.AppendLine($"        {actualVariableName}=new(__count{this.result});");
            builder.AppendLine($"        global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},__count{this.result});");
            builder.AppendLine($"        var __destination{this.result}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
            builder.AppendLine($"        if(!{this.EnumeratorName()}.TryCopyTo(__destination{this.result},0)) {{");
            builder.AppendLine($"            var __index{this.result}=0;");
            builder.AppendLine($"            while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) __destination{this.result}[__index{this.result}++]=__item{this.result};");
            builder.AppendLine("        }");
            builder.AppendLine($"    }}else{{");
            builder.AppendLine($"        {actualVariableName}=global::Seamless.Linq.GeneratedCodeHelpers.ToListFromValueEnumerator<{pipeline.EnumeratorTypeName},{pipeline.ElementType}>(ref {this.EnumeratorName()});");
            builder.AppendLine($"    }}");
            builder.AppendLine("}");
            builder.AppendLine("finally {");
            builder.AppendLine($"    {this.EnumeratorName()}.Dispose();");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToArray) {
            if(this.RenderDirectSpanPipelineToArray(actualVariableName,actualTypeName) is { } directSpanPipeline) {
                return directSpanPipeline;
            }
            var listName = $"{actualVariableName}_list";
            var countName = $"{actualVariableName}_count";
            var indexName = $"{actualVariableName}_index";
            var bufferName = $"{actualVariableName}_buffer";
            var nName = $"{actualVariableName}_n";
            var nextName = $"{actualVariableName}_next";
            var endLabel = $"__arrayEnd{this.result}";
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNonEnumeratedCount(out var {countName})) {{");
            builder.AppendLine($"    {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
            builder.AppendLine($"    if({this.EnumeratorName()}.TryCopyTo(global::System.MemoryExtensions.AsSpan({actualVariableName}),0)) goto {endLabel};");
            builder.AppendLine($"    var {indexName}=0;");
            builder.AppendLine($"    while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"        if((uint){indexName}>=(uint){actualVariableName}.Length) {{");
            builder.AppendLine($"            var {listName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({actualVariableName});");
            builder.AppendLine($"            do {{");
            builder.AppendLine($"                {listName}.Add(__item{this.result});");
            builder.AppendLine($"            }} while({this.EnumeratorName()}.TryGetNext(out __item{this.result}));");
            builder.AppendLine($"            {actualVariableName}={listName}.ToArray();");
            builder.AppendLine($"            goto {endLabel};");
            builder.AppendLine($"        }}");
            builder.AppendLine($"        {actualVariableName}[{indexName}++]=__item{this.result};");
            builder.AppendLine($"    }}");
            builder.AppendLine($"    if({indexName}!={actualVariableName}.Length) global::System.Array.Resize(ref {actualVariableName},{indexName});");
            builder.AppendLine($"}}else{{");
            builder.AppendLine($"    {pipeline.ElementType}[]? {bufferName}=null;");
            builder.AppendLine($"    var {nName}=0;");
            builder.AppendLine($"    try {{");
            builder.AppendLine($"        while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"            if({bufferName} is null) {{");
            builder.AppendLine($"                {bufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent(16);");
            builder.AppendLine($"            }} else if({nName}=={bufferName}.Length) {{");
            builder.AppendLine($"                var {nextName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({bufferName}.Length*2);");
            builder.AppendLine($"                global::System.Array.Copy({bufferName},{nextName},{nName});");
            builder.AppendLine($"                global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({bufferName},clearArray:true);");
            builder.AppendLine($"                {bufferName}={nextName};");
            builder.AppendLine($"            }}");
            builder.AppendLine($"            {bufferName}[{nName}++]=__item{this.result};");
            builder.AppendLine($"        }}");
            builder.AppendLine($"        if({nName}==0) {{");
            builder.AppendLine($"            {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            builder.AppendLine($"        }} else {{");
            builder.AppendLine($"            {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({nName});");
            builder.AppendLine($"            global::System.Array.Copy({bufferName}!,{actualVariableName},{nName});");
            builder.AppendLine($"        }}");
            builder.AppendLine($"    }} finally {{");
            builder.AppendLine($"        if({bufferName} is not null) global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({bufferName},clearArray:true);");
            builder.AppendLine($"    }}");
            builder.AppendLine($"}}");
            builder.AppendLine($"{endLabel}:;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Count) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out _)) {actualVariableName}++;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out _)) {actualVariableName}=true;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.First) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=__item{this.result};");
            builder.AppendLine("} else {");
            builder.AppendLine("    throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {actualVariableName}+=__item{this.result};");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary) {
            var func1Code = func1;
            var func2Code = func2;
            var cmpArg = comparerValue??"null";
            var keyType = func1Code?.ReturnTypeName??"object";
            var valType = func2Code?.ReturnTypeName??pipeline.ElementType;
            var dictTypeName = $"global::System.Collections.Generic.Dictionary<{keyType},{valType}>";
            var eName = this.EnumeratorName();
            var itemName = $"__dItem{this.result}";
            var keyName = $"__dKey{this.result}";
            var valName = $"__dVal{this.result}";
            builder.AppendLine($"using var {eName}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({eName}.TryGetNonEnumeratedCount(out var __count{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=new {dictTypeName}(__count{this.result},(global::System.Collections.Generic.IEqualityComparer<{keyType}>?){cmpArg});");
            builder.AppendLine($"}}else{{");
            builder.AppendLine($"    {actualVariableName}=new {dictTypeName}((global::System.Collections.Generic.IEqualityComparer<{keyType}>?){cmpArg});");
            builder.AppendLine($"}}");
            builder.AppendLine($"while({eName}.TryGetNext(out var {itemName})) {{");
            builder.AppendLine($"    {keyType} {keyName}=default!;");
            AppendIndented(builder,this.FuncAssign(func1Code!,"ToDictionary",keyName,itemName),"    ");
            if(func2Code is not null) {
                builder.AppendLine($"    {valType} {valName}=default!;");
                AppendIndented(builder,this.FuncAssign(func2Code,"ToDictionary",valName,itemName),"    ");
                builder.AppendLine($"    {actualVariableName}[{keyName}]={valName};");
            } else {
                builder.AppendLine($"    {actualVariableName}[{keyName}]=({valType})(object?){itemName}!;");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        throw new NotSupportedException($"Unsupported pipeline terminal: {terminalKind}.");
    }

    private string EnumeratorName() => $"__pipeline{this.result}";
    private static void AppendIndented(StringBuilder sb,string text,string indent) {
        GeneratedCodeWriter.AppendIndented(sb,text,indent);
    }
}
