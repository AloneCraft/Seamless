namespace Seamless.FLinq.Linq.Fragments;

public class OrderBy(int 番号, Enumerable source, FuncFragment keySelector) : OrderBy_comparer(番号, source, keySelector, "null") { }
public class OrderByDescending(int 番号, Enumerable source, FuncFragment keySelector) : OrderByDescending_comparer(番号, source, keySelector, "null") { }

public class OrderBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: false) { }
public class OrderByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderingBase(番号, source, keySelector, comparer, descending: true) { }

/*
OrderingBase は OrderBy/ThenBy 系を、全件 buffer + stable sort + buffer enumerator に変換する。

入力例:
  source.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)

生成例:
  var items = new List<SortItem>();
  foreach(var item in source) {
      items.Add(new SortItem(item, keySelector(item), originalIndex++));
  }
  items.Sort((left, right) => CompareKeysThenOriginalIndex(left, right));
  return items.Select(x => x.Value).ToList();

バリエーション:
  - descending=true なら比較結果の符号を反転する。
  - source が既に OrderingBase の場合、一次 key と二次 key を SortItem に同居させる。
  - originalIndex を最後に比較し、System.Linq と同じ安定ソートの結果にする。
*/
public abstract class OrderingBase(int 番号, Enumerable source, FuncFragment keySelector, string comparer, bool descending) : Enumerable(番号) {
    // チェーン（ThenBy）検出用に一次ソースを公開
    internal FuncFragment OwnKeySelector => keySelector;
    internal string OwnComparer => ComparerExpressionSupport.RenderOrderComparer(comparer,FuncReturnTypeName(keySelector));
    internal bool OwnDescending => descending;
    internal Enumerable OwnSource => source;

    // source が OrderingBase の場合、その一次ソートを取得
    private OrderingBase? PrimaryOrdering => source as OrderingBase;
    // ソート前の元データソース（OrderingBase チェーンを遡る）
    private Enumerable UnorderedSource {
        get {
            Enumerable s = source;
            while(s is OrderingBase oo) s = oo.OwnSource;
            return s;
        }
    }

    protected override Enumerable PrimarySource => UnorderedSource;
    public override string ElementType => UnorderedSource.ElementType;
    public override string GetCurrentExpression => currentValue;

    public override string StructMethod =>
        PrimaryOrdering is { } po
            ? JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)),FuncSupport(po.OwnKeySelector, "primaryKeySelector"))
            : FuncSupport(keySelector, nameof(keySelector));

    public override string GetEnumeratorStruct => $$"""
        {{UnorderedSource.GetEnumeratorStruct}}
        {{DefineEnumeratorStructCore(ElementType,GetEnumeratorDisposeBody)}}
        """;

    public override string GetEnumeratorExpression =>
        PrimaryOrdering is { } po
            ? $"new {EnumeratorTypeName}({UnorderedSource.GetEnumeratorExpression}, {OwnComparer}, {po.OwnComparer}{ClosureArgsSuffix(keySelector, po.OwnKeySelector)})"
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {OwnComparer}{ClosureArgsSuffix(keySelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        PrimaryOrdering is { } po
            ? $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(po.OwnKeySelector)}>? primaryComparer{ClosurePrimaryCtorSuffix(keySelector, po.OwnKeySelector)}"
            : $"TEnum source, global::System.Collections.Generic.IComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";

    public override string GetEnumeratorConstructor =>
        PrimaryOrdering is { } po
            ? $"this.Build(comparer,primaryComparer{ClosureArgsSuffixForCtorBody(keySelector, po.OwnKeySelector)});"
            : $"this.Build(comparer{ClosureArgsSuffixForCtorBody(keySelector)});";

    public override string GetEnumeratorFields =>
        PrimaryOrdering is { } po ? BuildMembersCombined(po) : BuildMembersSingle();

    private string BuildMembersSingle() => $$"""
        private {{ElementType}}[]? values;
        private {{FuncReturnTypeName(keySelector)}}[]? keys;
        private int[]? indexMap;
        private int count;
        private int position = -1;
        private readonly struct IndexComparer(global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}> comparer,{{FuncReturnTypeName(keySelector)}}[] keys) : global::System.Collections.Generic.IComparer<int> {
            public int Compare(int left,int right) {
                var compare=comparer.Compare(keys[left],keys[right]);
                if(compare!=0) return {{(descending ? "-compare" : "compare")}};
                return left.CompareTo(right);
            }
        }
        private static void ReturnArray<TArrayItem>(TArrayItem[]? array) {
            if(array is not null) {
                global::System.Buffers.ArrayPool<TArrayItem>.Shared.Return(array,clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TArrayItem>());
            }
        }
        private void DisposeBuffers() {
            ReturnArray(this.values);
            this.values=null;
            ReturnArray(this.keys);
            this.keys=null;
            if(this.indexMap is not null) {
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
                this.indexMap=null;
            }
            this.count=0;
            this.position=-1;
        }
        private void EnsureCapacity(int minimum) {
            if(this.values is not null && this.values.Length>=minimum) {
                return;
            }
            var capacity=this.values is null ? 4 : this.values.Length * 2;
            if(capacity<minimum) {
                capacity=minimum;
            }
            var newValues=global::System.Buffers.ArrayPool<{{ElementType}}>.Shared.Rent(capacity);
            var newKeys=global::System.Buffers.ArrayPool<{{FuncReturnTypeName(keySelector)}}>.Shared.Rent(capacity);
            var newIndexMap=global::System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
            if(this.count!=0) {
                global::System.Array.Copy(this.values!,newValues,this.count);
                global::System.Array.Copy(this.keys!,newKeys,this.count);
                global::System.Array.Copy(this.indexMap!,newIndexMap,this.count);
            }
            ReturnArray(this.values);
            ReturnArray(this.keys);
            if(this.indexMap is not null) {
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
            }
            this.values=newValues;
            this.keys=newKeys;
            this.indexMap=newIndexMap;
        }
        private void AddItem({{ElementType}} value,{{FuncReturnTypeName(keySelector)}} key) {
            this.EnsureCapacity(this.count+1);
            this.values![this.count]=value;
            this.keys![this.count]=key;
            this.indexMap![this.count]=this.count;
            this.count++;
        }
        private void Build(global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}>? comparer{{ClosureMethodParameterSuffix(keySelector)}}) {
            comparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            try {
                if(this.source.TryGetNonEnumeratedCount(out var __count) && __count>0) {
                    this.EnsureCapacity(__count);
                }
                {{SortLoopSingle}}
                if(this.count>1) {
                    global::System.Array.Sort(this.indexMap!,0,this.count,new IndexComparer(comparer,this.keys!));
                }
            }catch{
                this.DisposeBuffers();
                this.source.Dispose();
                throw;
            }
        }
        """;

    private string SortLoopSingle => UnorderedSource.CanProvideSpan
        ? $$"""
                this.source.TryGetSpan(out var __span);
                foreach(var {{item}} in __span) {
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssignIndented("                ",keySelector,nameof(keySelector),key,item)}}
                    this.AddItem({{item}},{{key}});
                }
            """
        : $$"""
                while(this.source.TryGetNext(out var {{item}})) {
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssignIndented("                ",keySelector,nameof(keySelector),key,item)}}
                    this.AddItem({{item}},{{key}});
                }
            """;

    private string BuildMembersCombined(OrderingBase po) => $$"""
        private {{ElementType}}[]? values;
        private {{FuncReturnTypeName(po.OwnKeySelector)}}[]? primaryKeys;
        private {{FuncReturnTypeName(keySelector)}}[]? keys;
        private int[]? indexMap;
        private int count;
        private int position = -1;
        private readonly struct IndexComparer(
            global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(po.OwnKeySelector)}}> primaryComparer,
            global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}> comparer,
            {{FuncReturnTypeName(po.OwnKeySelector)}}[] primaryKeys,
            {{FuncReturnTypeName(keySelector)}}[] keys) : global::System.Collections.Generic.IComparer<int> {
            public int Compare(int left,int right) {
                var primaryCompare=primaryComparer.Compare(primaryKeys[left],primaryKeys[right]);
                if(primaryCompare!=0) return {{(po.OwnDescending ? "-primaryCompare" : "primaryCompare")}};
                var compare=comparer.Compare(keys[left],keys[right]);
                if(compare!=0) return {{(descending ? "-compare" : "compare")}};
                return left.CompareTo(right);
            }
        }
        private static void ReturnArray<TArrayItem>(TArrayItem[]? array) {
            if(array is not null) {
                global::System.Buffers.ArrayPool<TArrayItem>.Shared.Return(array,clearArray: global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TArrayItem>());
            }
        }
        private void DisposeBuffers() {
            ReturnArray(this.values);
            this.values=null;
            ReturnArray(this.primaryKeys);
            this.primaryKeys=null;
            ReturnArray(this.keys);
            this.keys=null;
            if(this.indexMap is not null) {
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
                this.indexMap=null;
            }
            this.count=0;
            this.position=-1;
        }
        private void EnsureCapacity(int minimum) {
            if(this.values is not null && this.values.Length>=minimum) {
                return;
            }
            var capacity=this.values is null ? 4 : this.values.Length * 2;
            if(capacity<minimum) {
                capacity=minimum;
            }
            var newValues=global::System.Buffers.ArrayPool<{{ElementType}}>.Shared.Rent(capacity);
            var newPrimaryKeys=global::System.Buffers.ArrayPool<{{FuncReturnTypeName(po.OwnKeySelector)}}>.Shared.Rent(capacity);
            var newKeys=global::System.Buffers.ArrayPool<{{FuncReturnTypeName(keySelector)}}>.Shared.Rent(capacity);
            var newIndexMap=global::System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
            if(this.count!=0) {
                global::System.Array.Copy(this.values!,newValues,this.count);
                global::System.Array.Copy(this.primaryKeys!,newPrimaryKeys,this.count);
                global::System.Array.Copy(this.keys!,newKeys,this.count);
                global::System.Array.Copy(this.indexMap!,newIndexMap,this.count);
            }
            ReturnArray(this.values);
            ReturnArray(this.primaryKeys);
            ReturnArray(this.keys);
            if(this.indexMap is not null) {
                global::System.Buffers.ArrayPool<int>.Shared.Return(this.indexMap,clearArray: false);
            }
            this.values=newValues;
            this.primaryKeys=newPrimaryKeys;
            this.keys=newKeys;
            this.indexMap=newIndexMap;
        }
        private void AddItem({{ElementType}} value,{{FuncReturnTypeName(po.OwnKeySelector)}} primaryKey,{{FuncReturnTypeName(keySelector)}} key) {
            this.EnsureCapacity(this.count+1);
            this.values![this.count]=value;
            this.primaryKeys![this.count]=primaryKey;
            this.keys![this.count]=key;
            this.indexMap![this.count]=this.count;
            this.count++;
        }
        private void Build(global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(keySelector)}}>? comparer,global::System.Collections.Generic.IComparer<{{FuncReturnTypeName(po.OwnKeySelector)}}>? primaryComparer{{ClosureMethodParameterSuffix(keySelector, po.OwnKeySelector)}}) {
            comparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            primaryComparer ??= global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(po.OwnKeySelector)}}>.Default;
            try {
                if(this.source.TryGetNonEnumeratedCount(out var __count) && __count>0) {
                    this.EnsureCapacity(__count);
                }
                {{SortLoopCombined(po)}}
                if(this.count>1) {
                    global::System.Array.Sort(this.indexMap!,0,this.count,new IndexComparer(primaryComparer,comparer,this.primaryKeys!,this.keys!));
                }
            }catch{
                this.DisposeBuffers();
                this.source.Dispose();
                throw;
            }
        }
        """;

    private string SortLoopCombined(OrderingBase po) => UnorderedSource.CanProvideSpan
        ? $$"""
                this.source.TryGetSpan(out var __span);
                foreach(var {{item}} in __span) {
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssignIndented("                ",keySelector,nameof(keySelector),key,item)}}
                    {{FuncReturnTypeName(po.OwnKeySelector)}} {{a}}=default!;
                    {{FuncAssignIndented("                ",po.OwnKeySelector,"primaryKeySelector",a,item)}}
                    this.AddItem({{item}},{{a}},{{key}});
                }
            """
        : $$"""
                while(this.source.TryGetNext(out var {{item}})) {
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssignIndented("                ",keySelector,nameof(keySelector),key,item)}}
                    {{FuncReturnTypeName(po.OwnKeySelector)}} {{a}}=default!;
                    {{FuncAssignIndented("                ",po.OwnKeySelector,"primaryKeySelector",a,item)}}
                    this.AddItem({{item}},{{a}},{{key}});
                }
            """;

    protected override string GetTryGetNonEnumeratedCountBody => $$"""
        count=this.count-(this.position+1);
        if(count<0) {
            count=0;
        }
        return true;
        """;

    public override string GetTryGetNextBody => $$"""
        var next=this.position+1;
        if(next>=this.count || this.values is null || this.indexMap is null) {
            current=default!;
            return false;
        }
        this.position=next;
        current=this.Current=this.values[this.indexMap[next]];
        return true;
        """;

    protected override string GetEnumeratorDisposeBody => $$"""
        this.DisposeBuffers();
        this.source.Dispose();
        """;
}
