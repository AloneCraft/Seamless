namespace Seamless.FLinq.Generator;
/*
CountBy は key ごとの出現数を数え、KeyValuePair<TKey,int> 相当を列挙する Fragment。

入力例:
  source.CountBy(x => x.Category)

生成例:
  foreach(var item in source) {
      key = keySelector(item);
      counts[key] = counts.TryGetValue(key, out c) ? c + 1 : 1;
  }
  foreach(var pair in counts) current = pair;

設計意図:
  GroupBy(...).Select(g => (g.Key, g.Count())) より、group の List を作らず count だけを Dictionary に保持する。
*/
internal class CountBy(int 番号,Enumerable source,FuncFragment keySelector):Enumerable(番号) {
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
    }

    private bool UsesPooledIntMap => IsInt32Type(FuncReturnTypeName(keySelector));
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    private static string CapacityExpression(string sourceCountExpression) => sourceCountExpression;
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.CountBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => this.UsesPooledIntMap ? this.PooledIntMapFields : $$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>? {{this.lookup}};
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>.Enumerator {{this.innerEnumerator}};
        """;
    public override string GetTryGetNextBody => this.UsesPooledIntMap ? this.PooledIntMapTryGetNextBody : $$"""
        if(this.{{this.lookup}} is null){
            if({{this.e}}.TryGetSpan(out var __span)){
                this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>({{CapacityExpression("__span.Length")}});
                for(var __i=0;__i<__span.Length;__i++){
                    var {{this.item}}=__span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    __cnt++;
                }
            }else{
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)){
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>({{CapacityExpression("__count")}});
                }else{
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>();
                }
                while({{this.e}}.TryGetNext(out var __mn20)){
                    var {{this.item}}=__mn20;
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    __cnt++;
                }
            }
            this.{{this.innerEnumerator}}=this.{{this.lookup}}.GetEnumerator();
        }
        if(this.{{this.innerEnumerator}}.TryGetNext(out var __mn21)){
            var __kv=__mn21;
            current=this.{{this.currentValue}}=new global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>(__kv.Key,__kv.Value);
            return true;
        }
        current=default!;
        return false;
        """;
    protected override string GetEnumeratorDisposeBody => this.UsesPooledIntMap
        ? $$"""
            this.DisposePooledMap();
            {{this.e}}.Dispose();
            """
        : base.GetEnumeratorDisposeBody;
    public override string ElementType => $"global::System.Collections.Generic.KeyValuePair<{FuncReturnTypeName(keySelector)}, global::System.Int32>";

    // ソースが根(配列・List、または IEnumerable)なら、CountBy の列挙子の構造体を作らずに差し替えメソッドの中で根を直接回して
    // 辞書で数え、追加順(削除しない Dictionary の列挙順)に流す(列挙子の構造体→根の構造体の入れ子は、参照型の共有ジェネリックコードで
    // 内側がインライン化されない)。全件を数えてから流すのは列挙子と同じなので、キー選択子の評価の回数と順は変わらない。
    private string? RenderRootCountsConsume(string consumerName) {
        if(!(source.ProvidesRemainingEnumerator||source is ArrayRoot or ListRoot)) {
            return null;
        }
        string sourceExpression;
        try {
            sourceExpression=source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var keyType = FuncReturnTypeName(keySelector);
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(keySelector),
            this.StaticDelegateLocalDeclarations(keySelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var sourceName = $"__countBySource{番号}";
        var countName = $"__countByCount{番号}";
        var mapName = $"__countByMap{番号}";
        var itemName = $"__countByItem{番号}";
        var keyName = $"__countByKey{番号}";
        var add = $"{keyType} {keyName}=default!;\n{this.FuncAssignStatic(keySelector,nameof(keySelector),keyName,itemName)}\nglobal::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault({mapName},{keyName},out _)++;";
        var loop = source.GetDirectSpanExpression(sourceName) is { } spanExpression
            ? $$"""
                var {{sourceName}}Span={{spanExpression}};
                var {{mapName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},int>({{CapacityExpression($"{sourceName}Span.Length")}});
                for(var {{sourceName}}Index=0;{{sourceName}}Index<{{sourceName}}Span.Length;{{sourceName}}Index++){
                    var {{itemName}}={{sourceName}}Span[{{sourceName}}Index];
                {{GroupBySupport.IndentBlock(add,"    ")}}
                }
                """
            : $$"""
                var {{sourceName}}HasSpan={{sourceName}}.TryGetSpan(out var {{sourceName}}Span);
                var {{countName}}={{sourceName}}HasSpan ? {{sourceName}}Span.Length : {{sourceName}}.TryGetNonEnumeratedCount(out var {{countName}}Raw) ? {{countName}}Raw : 0;
                var {{mapName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},int>({{CapacityExpression(countName)}});
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
            foreach(var {{mapName}}Entry in {{mapName}}){
                {{consumerName}}.Consume(new global::System.Collections.Generic.KeyValuePair<{{keyType}},global::System.Int32>({{mapName}}Entry.Key,{{mapName}}Entry.Value));
            }
            """;
    }

    private string? RenderSourceCountsConsume(string consumerName) {
        var placeholder = $"__FLINQ_COUNTBY_SRC_{番号}__";
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
        var keyType = FuncReturnTypeName(keySelector);
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(keySelector),
            this.StaticDelegateLocalDeclarations(keySelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var mapName = $"__countByMap{番号}";
        var keyName = $"__countByKey{番号}";
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(sourceConsume,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            var assign = this.FuncAssignStatic(keySelector,nameof(keySelector),keyName,arg);
            return $"{indent}{keyType} {keyName}=default!;\n{indent}{assign}\n{indent}global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault({mapName},{keyName},out _)++;";
        });
        // 根が Range で間に Select などがあるとき、上流の消費が使う __rangeStart / __rangeCount の宣言(準備文)を先に出す。
        // 出さないと、これらが未宣言のまま使われて CS0103 になる(2026-10-22)。
        return $$"""
            {{source.GetConsumePreparationStatement()}}
            {{localDeclarations}}
            var {{mapName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},int>();
            {{replaced}}
            foreach(var {{mapName}}Entry in {{mapName}}){
                {{consumerName}}.Consume(new global::System.Collections.Generic.KeyValuePair<{{keyType}},global::System.Int32>({{mapName}}Entry.Key,{{mapName}}Entry.Value));
            }
            """;
    }

    // Range の CountBy(x => x%C) を Consume へ流すとき、start≧0 ならキーは start から先頭 min(count,C) 個で初めて現れ、
    // 値 x のキーの個数は [x,end) で x と合同な値の個数 (end-1-x)/C+1 になる。数え上げも辞書も要らない。
    // 負の値を含むとき(キーが負になる)は、Range の引数を1回だけ評価したうえで辞書で数える。
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(source is not RangeRoot range||!RangeModuloOrderRoot.TryGetModulus(keySelector,out var modulus)) {
            if(this.RenderSourceCountsConsume(consumerName) is { } sourceCounts) {
                return sourceCounts;
            }
            if(this.RenderRootCountsConsume(consumerName) is not { } rootCounts) {
                return base.GetConsumeExecutionStatement(consumerName);
            }
            // int のキーの専用の表(UsesPooledIntMap)は Dictionary より速いが、列挙子の構造体の入れ子は参照型の要素では
            // インライン化されない。要素が参照型(を含む)ときだけ根を直接回す形にする(判定は型ごとに JIT が定数に畳む)。
            return this.UsesPooledIntMap
                ? $$"""
                    if(global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{{source.ElementType}}>()){
                    {{GroupBySupport.IndentBlock(rootCounts,"    ")}}
                    }else{
                    {{GroupBySupport.IndentBlock(base.GetConsumeExecutionStatement(consumerName),"    ")}}
                    }
                    """
                : rootCounts;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var end = $"__end{番号}";
        var value = $"__value{番号}";
        var counts = $"__counts{番号}";
        var order = $"__order{番号}";
        var key = $"__key{番号}";
        return $$"""
            {
                {{GroupBySupport.IndentBlock(range.RenderArgumentPreparation(start,count),"    ").TrimStart()}}
                if({{start}}>=0){
                    var {{end}}=(long){{start}}+{{count}};
                    var __length{{番号}}=global::System.Math.Min({{count}},{{modulus}});
                    for(var {{value}}=(long){{start}};{{value}}<(long){{start}}+__length{{番号}};{{value}}++){
                        {{consumerName}}.Consume(new global::System.Collections.Generic.KeyValuePair<int,int>((int)({{value}}%{{modulus}}),(int)(({{end}}-1-{{value}})/{{modulus}}+1)));
                    }
                }else{
                    var {{counts}}=new global::System.Collections.Generic.Dictionary<int,int>();
                    var {{order}}=new global::System.Collections.Generic.List<int>();
                    for(var {{value}}=(long){{start}};{{value}}<(long){{start}}+{{count}};{{value}}++){
                        var {{key}}=(int)({{value}}%{{modulus}});
                        if({{counts}}.TryGetValue({{key}},out var __seen{{番号}})){
                            {{counts}}[{{key}}]=__seen{{番号}}+1;
                        }else{
                            {{counts}}[{{key}}]=1;
                            {{order}}.Add({{key}});
                        }
                    }
                    foreach(var {{key}} in {{order}}){
                        {{consumerName}}.Consume(new global::System.Collections.Generic.KeyValuePair<int,int>({{key}},{{counts}}[{{key}}]));
                    }
                }
            }
            """;
    }
    private string PooledIntMapFields => $$"""
        private int[]? __hashKeys;
        private int[]? __hashCounts;
        private int[]? __hashOrder;
        private byte[]? __hashUsed;
        private int __hashCapacity;
        private int __hashEntryCount;
        private int __hashEnumerationIndex;
        private void InitializePooledMap(int minimumCapacity){
            var capacity=16;
            var required=minimumCapacity<=0 ? 16L : global::System.Math.Min(1L<<30,(long)minimumCapacity*2);
            while(capacity<required&&capacity<(1<<30)){
                capacity<<=1;
            }
            this.__hashKeys=global::System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
            this.__hashCounts=global::System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
            this.__hashOrder=global::System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
            this.__hashUsed=global::System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
            global::System.MemoryExtensions.AsSpan(this.__hashUsed,0,capacity).Clear();
            this.__hashCapacity=capacity;
            this.__hashEntryCount=0;
            this.__hashEnumerationIndex=0;
        }
        private int FindPooledMapSlot(int key){
            var slot=(int)(unchecked((uint)key*2654435761u)&(uint)(this.__hashCapacity-1));
            while(this.__hashUsed![slot]!=0&&this.__hashKeys![slot]!=key){
                slot=(slot+1)&(this.__hashCapacity-1);
            }
            return slot;
        }
        private void GrowPooledMap(){
            var oldKeys=this.__hashKeys!;
            var oldCounts=this.__hashCounts!;
            var oldOrder=this.__hashOrder!;
            var oldUsed=this.__hashUsed!;
            var oldCapacity=this.__hashCapacity;
            var oldEntryCount=this.__hashEntryCount;
            this.InitializePooledMap(oldCapacity);
            for(var index=0;index<oldEntryCount;index++){
                var oldSlot=oldOrder[index];
                var key=oldKeys[oldSlot];
                var slot=this.FindPooledMapSlot(key);
                this.__hashUsed![slot]=1;
                this.__hashKeys![slot]=key;
                this.__hashCounts![slot]=oldCounts[oldSlot];
                this.__hashOrder![this.__hashEntryCount++]=slot;
            }
            global::System.Buffers.ArrayPool<int>.Shared.Return(oldKeys,false);
            global::System.Buffers.ArrayPool<int>.Shared.Return(oldCounts,false);
            global::System.Buffers.ArrayPool<int>.Shared.Return(oldOrder,false);
            global::System.Buffers.ArrayPool<byte>.Shared.Return(oldUsed,true);
        }
        private void AddPooledMap(int key){
            if(this.__hashKeys is null){
                this.InitializePooledMap(0);
            }
            if((long)(this.__hashEntryCount+1)*10>=(long)this.__hashCapacity*7){
                this.GrowPooledMap();
            }
            var slot=this.FindPooledMapSlot(key);
            if(this.__hashUsed![slot]!=0){
                this.__hashCounts![slot]++;
                return;
            }
            this.__hashUsed[slot]=1;
            this.__hashKeys![slot]=key;
            this.__hashCounts![slot]=1;
            this.__hashOrder![this.__hashEntryCount++]=slot;
        }
        private void DisposePooledMap(){
            if(this.__hashKeys is not null){
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.__hashKeys,false);
                this.__hashKeys=null;
            }
            if(this.__hashCounts is not null){
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.__hashCounts,false);
                this.__hashCounts=null;
            }
            if(this.__hashOrder is not null){
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.__hashOrder,false);
                this.__hashOrder=null;
            }
            if(this.__hashUsed is not null){
                global::System.Buffers.ArrayPool<byte>.Shared.Return(this.__hashUsed,true);
                this.__hashUsed=null;
            }
            this.__hashCapacity=0;
            this.__hashEntryCount=0;
            this.__hashEnumerationIndex=0;
        }
        """;

    private string PooledIntMapTryGetNextBody => $$"""
        if(this.__hashKeys is null){
            if({{this.e}}.TryGetNonEnumeratedCount(out var __count)){
                this.InitializePooledMap(global::System.Math.Min(__count,1024));
            }else{
                this.InitializePooledMap(0);
            }
            if({{this.e}}.TryGetSpan(out var __span)){
                for(var __i=0;__i<__span.Length;__i++){
                    var {{this.item}}=__span[__i];
                    int {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    this.AddPooledMap({{this.key}});
                }
            }else{
                while({{this.e}}.TryGetNext(out var __mn22)){
                    var {{this.item}}=__mn22;
                    int {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    this.AddPooledMap({{this.key}});
                }
            }
        }
        if(this.__hashEnumerationIndex<this.__hashEntryCount){
            var __slot=this.__hashOrder![this.__hashEnumerationIndex++];
            current=this.{{this.currentValue}}=new global::System.Collections.Generic.KeyValuePair<int,global::System.Int32>(this.__hashKeys[__slot],this.__hashCounts![__slot]);
            return true;
        }
        current=default!;
        return false;
        """;

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        ||string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        ||string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}
