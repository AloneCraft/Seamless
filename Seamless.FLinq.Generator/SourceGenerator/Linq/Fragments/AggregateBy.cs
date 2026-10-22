namespace Seamless.FLinq.Generator;
/*
AggregateBy は key ごとに accumulator を持つ grouping 集約を生成する。

入力例:
  source.AggregateBy(x => x.GroupId, seed, (acc, item) => Step(acc, item))

生成例:
  foreach(var item in source) {
      key = keySelector(item);
      if(!map.TryGetValue(key, out acc)) acc = seed;
      map[key] = func(acc, item);
  }
  foreach(var pair in map) current = pair;

バリエーション:
  - keySelector/func はそれぞれ FuncFragment として closure/delegate/inline に対応する。
  - resultElementType は返す KeyValuePair 相当の要素型を決める。
*/
internal class AggregateBy(int 番号,Enumerable source,FuncFragment keySelector,string seed,FuncFragment func,string resultElementType):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
        if(func is { } __visitorChild_func) {
            yield return __visitorChild_func;
        }
    }

    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private static string CapacityExpression(string sourceCountExpression) =>
        $"global::System.Math.Min({sourceCountExpression},1024)";

    public override string StructMethod => $$"""
        {{this.FuncSupport(keySelector,nameof(keySelector))}}
        {{this.FuncSupport(func,nameof(func))}}
        """;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.AggregateBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()}, {seed}, {func.DelegateConstructorArg??func.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector,func },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,func)}";
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>? {{this.lookup}};
        private global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>.Enumerator {{this.innerEnumerator}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.lookup}} is null){
            if({{this.e}}.TryGetSpan(out var __span)){
                this.{{this.lookup}}=new global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>({{CapacityExpression("__span.Length")}});
                for(var __i=0;__i<__span.Length;__i++){
                    var {{this.item}}=__span[__i];
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref this.{{this.lookup}}.GetValueRefOrAddDefault({{this.key}},out var __exists);
                    if(!__exists){
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }else{
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)){
                    this.{{this.lookup}}=new global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>({{CapacityExpression("__count")}});
                }else{
                    this.{{this.lookup}}=new global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{FuncReturnTypeName(func)}}>();
                }
                while({{this.e}}.TryGetNext(out var __mn0)){
                    var {{this.item}}=__mn0;
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref this.{{this.lookup}}.GetValueRefOrAddDefault({{this.key}},out var __exists);
                    if(!__exists){
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }
            this.{{this.innerEnumerator}}=this.{{this.lookup}}.GetEnumerator();
        }
        if(this.{{this.innerEnumerator}}.TryGetNext(out var __kv)){
            current=this.{{this.currentValue}}=new {{resultElementType}}(__kv.Key,__kv.Value);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => resultElementType;
    protected override string GetEnumeratorDisposeBody => $"this.{this.lookup}?.Dispose(); {this.e}.Dispose();";

    private string? RenderSourceAggregateConsume(string consumerName) {
        var placeholder = $"__FLINQ_AGGBY_SRC_{番号}__";
        string sourceConsume;
        try {
            sourceConsume=source.GetConsumeExecutionStatement(placeholder);
        } catch(NotSupportedException) {
            return null;
        }
        if(string.IsNullOrWhiteSpace(sourceConsume)
            ||sourceConsume.Contains("TryGetNext")
            ||sourceConsume.Contains("GetRemainingEnumerator")
            ||!sourceConsume.Contains($"{placeholder}.Consume(")) {
            return null;
        }
        var valueType = FuncReturnTypeName(func);
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(keySelector,func),
            this.StaticDelegateLocalDeclarations(keySelector,func),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var mapName = $"__aggregateByMap{番号}";
        var keyName = $"__aggregateByKey{番号}";
        var valueName = $"__aggregateByValue{番号}";
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(sourceConsume,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            var assignKey = this.FuncAssignStatic(keySelector,nameof(keySelector),keyName,arg);
            var assignVal = this.FuncAssignStatic(func,nameof(func),valueName,valueName,arg);
            return $"{indent}{this.KeyType} {keyName}=default!;\n{indent}{assignKey}\n{indent}ref var {valueName}=ref {mapName}.GetValueRefOrAddDefault({keyName},out var {valueName}Exists);\n{indent}if(!{valueName}Exists){{\n{indent}    {valueName}={seed};\n{indent}}}\n{indent}{assignVal}";
        });
        // 根が Range で間に Select などがあるとき、上流の消費が使う __rangeStart / __rangeCount の宣言(準備文)を先に出す(CS0103 の対策)。
        return $$"""
            {{source.GetConsumePreparationStatement()}}
            {{localDeclarations}}
            var {{mapName}}=new global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{valueType}}>();
            try{
            {{replaced}}
                for(var {{mapName}}Index=0;{{mapName}}Index<{{mapName}}.Count;{{mapName}}Index++){
                    {{mapName}}.GetKeyAndValue({{mapName}}Index,out var {{mapName}}Key,out var {{mapName}}Value);
                    {{consumerName}}.Consume(new {{resultElementType}}({{mapName}}Key,{{mapName}}Value));
                }
            }finally{
                {{mapName}}.Dispose();
            }
            """;
    }

    // ソースが根(配列・List、または IEnumerable)なら、AggregateBy の列挙子の構造体を作らずに差し替えメソッドの中で根を直接回して
    // 集計し、追加順に流す(列挙子の構造体→根の構造体の入れ子は、参照型の共有ジェネリックコードで内側がインライン化されない)。
    // 全件を集計してから流すのは列挙子と同じなので、選択子の評価の回数と順は変わらない。
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.RenderSourceAggregateConsume(consumerName) is { } sourceConsume) {
            return sourceConsume;
        }
        if(!(source.ProvidesRemainingEnumerator||source is ArrayRoot or ListRoot)) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        string sourceExpression;
        try {
            sourceExpression=source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var valueType = FuncReturnTypeName(func);
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(keySelector,func),
            this.StaticDelegateLocalDeclarations(keySelector,func),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var sourceName = $"__aggregateBySource{番号}";
        var countName = $"__aggregateByCount{番号}";
        var mapName = $"__aggregateByMap{番号}";
        var itemName = $"__aggregateByItem{番号}";
        var keyName = $"__aggregateByKey{番号}";
        var valueName = $"__aggregateByValue{番号}";
        var add = $"{this.KeyType} {keyName}=default!;\n{this.FuncAssignStatic(keySelector,nameof(keySelector),keyName,itemName)}\n"
            +$"ref var {valueName}=ref {mapName}.GetValueRefOrAddDefault({keyName},out var {valueName}Exists);\n"
            +$"if(!{valueName}Exists){{\n    {valueName}={seed};\n}}\n"
            +this.FuncAssignStatic(func,nameof(func),valueName,valueName,itemName);
        var loop = source.GetDirectSpanExpression(sourceName) is { } spanExpression
            ? $$"""
                var {{sourceName}}Span={{spanExpression}};
                var {{mapName}}=new global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{valueType}}>({{CapacityExpression($"{sourceName}Span.Length")}});
                for(var {{sourceName}}Index=0;{{sourceName}}Index<{{sourceName}}Span.Length;{{sourceName}}Index++){
                    var {{itemName}}={{sourceName}}Span[{{sourceName}}Index];
                {{GroupBySupport.IndentBlock(add,"    ")}}
                }
                """
            : $$"""
                var {{sourceName}}HasSpan={{sourceName}}.TryGetSpan(out var {{sourceName}}Span);
                var {{countName}}={{sourceName}}HasSpan ? {{sourceName}}Span.Length : {{sourceName}}.TryGetNonEnumeratedCount(out var {{countName}}Raw) ? {{countName}}Raw : 0;
                var {{mapName}}=new global::Seamless.Linq.DictionarySlim<{{this.KeyType}},{{valueType}}>({{CapacityExpression(countName)}});
                if({{sourceName}}HasSpan){
                    for(var {{sourceName}}Index=0;{{sourceName}}Index<{{sourceName}}Span.Length;{{sourceName}}Index++){
                        var {{itemName}}={{sourceName}}Span[{{sourceName}}Index];
                    {{GroupBySupport.IndentBlock(add,"        ")}}
                    }
                }else{
                    {{DrainLoopHead(source,sourceName,itemName)}}
                    {{GroupBySupport.IndentBlock(add,"        ")}}
                    }
                }
                """;
        return $$"""
            {{localDeclarations}}
            using var {{sourceName}}={{sourceExpression}};
            {{loop}}
            try{
                for(var {{mapName}}Index=0;{{mapName}}Index<{{mapName}}.Count;{{mapName}}Index++){
                    {{mapName}}.GetKeyAndValue({{mapName}}Index,out var {{mapName}}Key,out var {{mapName}}Value);
                    {{consumerName}}.Consume(new {{resultElementType}}({{mapName}}Key,{{mapName}}Value));
                }
            }finally{
                {{mapName}}.Dispose();
            }
            """;
    }

}
