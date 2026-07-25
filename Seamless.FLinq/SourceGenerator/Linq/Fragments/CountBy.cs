namespace Seamless.FLinq.Linq.Fragments;
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
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector)})";
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
                this.InitializePooledMap(__count);
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
