namespace Seamless.FLinq;

/*
Root 系 Fragment は LINQ チェーンの最初の source を struct enumerator 化する。

入力例:
  array.Where(...)
  list.Select(...)
  Enumerable.Range(start, count)

生成されるコード例:
  private struct RootEnumerator {
      private int index;
      public bool TryGetNext(out T current) {
          if(++index < source.Length) {
              current = source[index];
              return true;
          }
          return false;
      }
  }

バリエーション:
  - Root: 任意の GetEnumerator() を包む。IEnumerable<T> interface 経由なら実行時に array/list span を試す。
  - ArrayRoot/ListRoot: index 直走査 + TryGetSpan/TryCopyTo を常に提供する。
  - RangeRoot/RepeatRoot/SequenceRoot: 元の collection を持たず、値を計算で生成する。
*/
// 以下 3 つは Root 系専用の基底。派生はこのファイル内の Root / ArrayRoot / ListRoot /
// PersistedListRoot だけなので、Bases ではなく本体ファイルへ置く（AI_SYNTAX_RULES の
// File Layout Rules: 実質 1 系列専用の基底は対応する本体ファイルへ集約する）。
// 複数ファイルから派生される ProviderNativeRootBase だけが Bases に残っている。
internal abstract class SourceRootBase(int number,string sourceExpression):Enumerable(number) {
    public string SourceExpression { get; } = sourceExpression;
    public abstract SourceCapability Capabilities { get; }
    public bool HasCapability(SourceCapability capability) => (this.Capabilities&capability)==capability;
}

internal abstract class EnumeratorRootBase(
    int number,
    string sourceExpression):SourceRootBase(number,sourceExpression);

internal abstract class DirectSpanRootBase(
    int number,
    string sourceExpression,
    string elementType):SourceRootBase(number,sourceExpression) {
    protected abstract string SourceTypeName { get; }
    protected abstract string CountExpression(string sourceName);
    protected abstract string RemainingCountExpression(string sourceName,string indexName);
    protected abstract string SpanExpression(string sourceName,string startExpression);
    protected virtual string IndexTypeName => "int";
    protected virtual string ElementExpression(string sourceName,string indexName) => $"{sourceName}[{indexName}]";

    public override SourceCapability Capabilities =>
        SourceCapability.KnownCount|SourceCapability.DirectSpan|SourceCapability.Indexable|SourceCapability.ChunkStreaming;
    public override bool CanProvideSpan => true;
    public override string? GetDirectSpanExpression(string enumeratorName) => $"{enumeratorName}.GetSpan()";
    public override string? NonEnumeratedCountExpression => this.CountExpression(this.SourceExpression);
    public override bool CountPreserving => true;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,this.SourceExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"{this.SourceTypeName} source";
    public override string GetEnumeratorFields => $"private {this.IndexTypeName} {this.e}=-1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}}<{{this.CountExpression("source")}}){
            current={{this.ElementExpression("source",this.e)}};
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}}{{this.StructTypeParameters}}:global::Seamless.Linq.IValueEnumerator<{{elementType}}>{
            private {{this.SourceTypeName}} source;
            private {{this.IndexTypeName}} {{this.e}}=-1;
            public {{this.Struct}}({{this.SourceTypeName}} source){this.source=source;}
            public {{elementType}} Current{get;private set;}=default!;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count){
                count={{this.RemainingCountExpression("source",this.e)}};
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span){
                span=this.GetSpan();
                return true;
            }
            {{AggressiveInliningAttribute}}
            public global::System.ReadOnlySpan<{{elementType}}> GetSpan()
                =>{{this.SpanExpression("source",$"{this.e}+1")}};
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination,global::System.Index offset){
                var remaining=this.GetSpan();
                var start=offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current){
                if(++{{this.e}}<{{this.CountExpression("source")}}){
                    current=this.Current={{this.ElementExpression("source",this.e)}};
                    return true;
                }
                current=default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
}

// Root は他の Fragment へ分解されない列挙ソースそのものを表す。
// enumeratorTypeName には具象 GetEnumerator() 戻り値型名を渡す。
// IEnumerable<T>/配列の場合は IEnumerator<T>、List<T> 等の具象型の場合は List<T>.Enumerator 等の値型エニュメレータ名になる。
// 構造体 Struct1<TEnum> は TEnum でパラメータ化され、呼び出し側で具体型引数により単相化される。
internal class Root(int 番号,string sourceExpression,string elementType,string enumeratorTypeName,bool useEnumerableInterface):EnumeratorRootBase(番号,sourceExpression) {
    public override SourceCapability Capabilities => SourceCapability.ChunkStreaming;
    public override bool CanProvideSpan => false;
    public override bool CanTryProvideSpan => useEnumerableInterface;
    public override string StructMethod => string.Empty;
    // 型引数を埋め込んだ完全な struct 名（呼び出し側では具象型引数を持つ）。
    // 含有型がジェネリック（OuterTypeParameters 有り）なら外側型パラメータを先頭に付ける。
    public override string EnumeratorTypeName => useEnumerableInterface
        ? this.EnumeratorTypeNameWithOuter()
        : this.EnumeratorTypeNameWithOuter(enumeratorTypeName);
    public override string? DirectEnumerableExpression => useEnumerableInterface
        ? $"((global::System.Collections.Generic.IEnumerable<{elementType}>)({sourceExpression}))"
        : null;
    public override string GetEnumeratorExpression {
        get {
            if(useEnumerableInterface) {
                return RenderEnumeratorExpression(this.EnumeratorTypeName,$"((global::System.Collections.Generic.IEnumerable<{elementType}>)({sourceExpression}))");
            }
            var sourceExpr = sourceExpression;
            return RenderEnumeratorExpression(this.EnumeratorTypeName,$"{sourceExpr}.GetEnumerator()");
        }
    }
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(!useEnumerableInterface) {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        // 静的 IEnumerable<T> 経路は型回復を試みない。終端 Consume では値列挙子の
        // ラッパーを生成せず、契約どおりの IEnumerator<T> を直接 1 回だけ取得して走査する。
        var sourceName=$"__interfaceSource{番号}";
        var enumeratorName=$"__interfaceEnumerator{番号}";
        var itemName=$"__interfaceItem{番号}";
        return $$"""
            {
                var {{sourceName}}=((global::System.Collections.Generic.IEnumerable<{{elementType}}>)({{this.SourceExpression}}));
                using var {{enumeratorName}}={{sourceName}}.GetEnumerator();
                while({{enumeratorName}}.MoveNext()){
                    var {{itemName}}={{enumeratorName}}.Current;
                    {{consumerName}}.Consume({{itemName}});
                }
            }
            """;
    }

    // TEnum でパラメータ化された struct を手動で出力する（where 制約が struct 宣言の opening brace より前に来る必要があるため）。
    public override string GetEnumeratorStruct => useEnumerableInterface
        ? this.GetEnumerableInterfaceStruct()
        : $$"""
        private struct {{this.Struct}}{{this.StructTypeParametersWithOuter("TEnum")}} : global::Seamless.Linq.IValueEnumerator<{{elementType}}>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private TEnum {{this.e}};
            public {{this.Struct}}(TEnum {{this.e}}) { this.{{this.e}} = {{this.e}}; }
            public {{elementType}} Current{get;private set;}=default!;
            public bool TryGetNonEnumeratedCount(out int count) { count = 0; return false; }
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination,global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current){
                if({{this.e}}.MoveNext()){
                    current = this.Current = {{this.e}}.Current;
                    return true;
                }
                current=default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){
                {{this.e}}.Dispose();
            }
        }
        """;

    private string GetEnumerableInterfaceStruct() => $$"""
        private struct {{this.Struct}}{{this.StructTypeParameters}} : global::Seamless.Linq.IValueEnumerator<{{elementType}}>
        {
            private global::System.Collections.Generic.IEnumerable<{{elementType}}> source;
            private global::System.Collections.Generic.IEnumerator<{{elementType}}>? {{this.e}};
            private int __index=-1;
            public {{this.Struct}}(global::System.Collections.Generic.IEnumerable<{{elementType}}> source) { this.source = source; }
            public {{elementType}} Current{get;private set;}=default!;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count){
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var sourceCount)){
                    count = sourceCount - (__index + 1);
                    return true;
                }
                count=0;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span){
                var start = __index + 1;
                if(source is {{elementType}}[] array){
                    span = global::System.MemoryExtensions.AsSpan(array, start);
                    return true;
                }
                if(source is global::System.Collections.Generic.List<{{elementType}}> list){
                    span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
                    return true;
                }
                span=default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination,global::System.Index offset){
                if(!TryGetSpan(out var span)){
                    return false;
                }
                var start = offset.GetOffset(span.Length);
                span.Slice(start).CopyTo(destination);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current){
                this.{{this.e}} ??= source.GetEnumerator();
                if(this.{{this.e}}.MoveNext()){
                    __index++;
                    current = this.Current = this.{{this.e}}.Current;
                    return true;
                }
                current=default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){
                this.{{this.e}}?.Dispose();
            }
        }
        """;
    // GetEnumeratorStruct を手動出力しているのでこれらは使われないが実装が必要。
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()){
            current = this.Current = {{this.e}}.Current;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => elementType;
}

// 配列ソース専用。インデックスベースで走査し、IEnumerator<T> を一切介さない完全具象 struct を生成する。
// TryGetSpan / TryGetNonEnumeratedCount も正しく実装するため GetEnumeratorStruct を手動出力する。
/*
ArrayRoot は配列 source 専用。IEnumerator<T> を作らず index で直接走査する。

生成例:
  if(++index < source.Length) {
      current = source[index];
      return true;
  }

設計意図:
  TryGetSpan/TryCopyTo/TryGetNonEnumeratedCount を常に true にできるので、後続の ToArray/Count などが
  分岐なしに連続メモリ fast path を選べる。
*/
internal sealed class ArrayRoot(int 番号,string sourceExpression,string elementType):DirectSpanRootBase(番号,sourceExpression,elementType) {
    public override string? DirectArrayExpression => sourceExpression;
    protected override string SourceTypeName => $"{elementType}[]";
    protected override string CountExpression(string sourceName) => $"{sourceName}.Length";
    protected override string RemainingCountExpression(string sourceName,string indexName) => $"{sourceName}.Length-({indexName}+1)";
    protected override string SpanExpression(string sourceName,string startExpression) => $"global::System.MemoryExtensions.AsSpan({sourceName},{startExpression})";
}

// List<T> ソース専用。List.Enumerator を包む素通し Root + バージョンチェックを避け、
// CollectionsMarshal.AsSpan(list) で内部配列を直接 Span として走査する（配列ソースと同等の高速パス）。
// 内部フィールド名に依存せず public API のみを使うため安全。TryGetSpan/TryGetNonEnumeratedCount/TryCopyTo も提供する。
/*
ListRoot は List<T> source 専用。List<T>.Enumerator の version check を避け、
CollectionsMarshal.AsSpan(list) を index で読む。

生成例:
  var span = CollectionsMarshal.AsSpan(source);
  if(++index < span.Length) {
      current = span[index];
      return true;
  }

設計意図:
  配列同様に TryGetSpan/TryCopyTo を提供し、Select/Where 後の materialize でもコピー系 fast path を維持する。
*/
internal sealed class ListRoot(int 番号,string sourceExpression,string elementType):DirectSpanRootBase(番号,sourceExpression,elementType) {
    protected override string SourceTypeName => $"global::System.Collections.Generic.List<{elementType}>";
    protected override string CountExpression(string sourceName) => $"{sourceName}.Count";
    protected override string RemainingCountExpression(string sourceName,string indexName) => $"{sourceName}.Count-({indexName}+1)";
    protected override string SpanExpression(string sourceName,string startExpression) => $"global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({sourceName}).Slice({startExpression})";
}

// IEnumerator<T> を実装しない duck-typing struct エニュメレータ（例: ImmutableArray<T>.Enumerator）向け。
// 具象エニュメレータ型を直接フィールドとして持つ非ジェネリック struct を生成する（直接展開）。
// TEnum の struct 制約が不要なため CS0315 が発生しない。
// Dispose() は no-op。duck-typing で IDisposable も実装するなら IEnumerator<T> も実装するはずで、
// その場合は通常の Root パスで処理されるため、ここには到達しない。
internal sealed class PersistedListRoot(int 番号,string sourceExpression,string elementType):DirectSpanRootBase(番号,sourceExpression,elementType) {
    protected override string SourceTypeName => $"global::Seamless.Collections.PersistedList<{elementType}>";
    protected override string IndexTypeName => "long";
    protected override string CountExpression(string sourceName) => $"{sourceName}.Count";
    protected override string RemainingCountExpression(string sourceName,string indexName) => $"checked((int)({sourceName}.Count-({indexName}+1)))";
    protected override string SpanExpression(string sourceName,string startExpression) => $"{sourceName}.AsSpan({startExpression},checked((int)({sourceName}.Count-({startExpression}))))";
}

internal class DuckTypingRoot(int 番号,string sourceExpression,string elementType,string concreteEnumeratorType):Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string EnumeratorTypeName => this.Struct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.Struct,sourceExpression+".GetEnumerator()");
    public override string GetEnumeratorPrimaryConstructorParameters => $"{concreteEnumeratorType} {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()){
            current = this.Current = {{this.e}}.Current;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Seamless.Linq.IValueEnumerator<{{elementType}}>
        {
            private {{concreteEnumeratorType}} {{this.e}};
            public {{this.Struct}}({{concreteEnumeratorType}} {{this.e}}) { this.{{this.e}} = {{this.e}}; }
            public {{elementType}} Current{get;private set;}=default!;
            public bool TryGetNonEnumeratedCount(out int count) { count = 0; return false; }
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination,global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current){
                if({{this.e}}.MoveNext()){
                    current = this.Current = {{this.e}}.Current;
                    return true;
                }
                current=default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
}

/*
RangeRoot は Enumerable.Range(start, count) を collection なしで生成する。

生成例:
  if(index < count) {
      current = start + index++;
      return true;
  }

バリエーション:
  - Count/Any/First/Single/ToArray/ToList などは RangePredicateOptimizer や専用 inline でさらに短い式になる。
  - DescendingRangeRoot は Reverse(Range(...)) などで逆順計算に寄せる。
*/
internal sealed class RangeRoot(int 番号,string startExpression,string countExpression):Enumerable(番号) {
    internal string StartExpression => startExpression;
    internal string CountExpression => countExpression;
    public override string? NonEnumeratedCountExpression => countExpression;
    public override bool CountPreserving => true;
    public override string StructMethod => string.Empty;
    public override string EnumeratorTypeName => this.Struct;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Range({startExpression}, {countExpression})";
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.Struct,startExpression,countExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => "int start, int count";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}} < count){
            current = this.Current = start + {{this.e}};
            return true;
        }
        current = default;
        return false;
        """;
    public override string ElementType => "int";
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Seamless.Linq.IValueEnumerator<int>
        {
            private int start;
            private int count;
            private int {{this.e}} = -1;
            public {{this.Struct}}(int start,int count) { this.start = start; this.count = count; }
            public int Current{get;private set;}=default!;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count){
                count = this.count - ({{this.e}} + 1);
                return true;
            }
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<int> destination,global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out int current){
                if(++{{this.e}} < count){
                    current = this.Current = start + {{this.e}};
                    return true;
                }
                current = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => $$"""
        {
            var __start{{番号}}={{startExpression}};
            var __count{{番号}}={{countExpression}};
            for(var __i{{番号}}=0;__i{{番号}}<__count{{番号}};__i{{番号}}++){
                {{consumerName}}.Consume(__start{{番号}} + __i{{番号}});
            }
        }
        """;
}

internal sealed class DescendingRangeRoot(int 番号,string startExpression,string countExpression):Enumerable(番号) {
    public override string? NonEnumeratedCountExpression => countExpression;
    public override bool CountPreserving => true;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Range({startExpression}, {countExpression}).Reverse()";
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.Struct,startExpression,countExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => "int start, int count";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}} < count){
            current = this.Current = start + count - 1 - {{this.e}};
            return true;
        }
        current = default;
        return false;
        """;
    public override string ElementType => "int";
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Seamless.Linq.IValueEnumerator<int>
        {
            private int start;
            private int count;
            private int {{this.e}} = -1;
            public {{this.Struct}}(int start,int count) { this.start = start; this.count = count; }
            public int Current{get;private set;}=default!;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count){
                count = this.count - ({{this.e}} + 1);
                return true;
            }
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<int> destination,global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out int current){
                if(++{{this.e}} < count){
                    current = this.Current = start + count - 1 - {{this.e}};
                    return true;
                }
                current = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => $$"""
        {
            var __start{{番号}}={{startExpression}};
            var __count{{番号}}={{countExpression}};
            for(var __i{{番号}}=__count{{番号}}-1;__i{{番号}}>=0;__i{{番号}}--){
                {{consumerName}}.Consume(__start{{番号}} + __i{{番号}});
            }
        }
        """;
}

/*
EmptyRoot は Enumerable.Empty<T>() を「常に false を返す enumerator」として生成する。

生成例:
  current = default!;
  return false;

設計意図:
  空列挙は Consume direct execution では空文字列を返し、呼び出し元に不要な while を出さない。
*/
internal sealed class EmptyRoot(int 番号,string elementType):Enumerable(番号) {
    public override string? NonEnumeratedCountExpression => "0";
    public override bool CountPreserving => true;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Empty<{elementType}>()";
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName);
    public override string GetEnumeratorPrimaryConstructorParameters => string.Empty;
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        current=default!;
        return false;
        """;
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}}{{this.StructTypeParameters}} : global::Seamless.Linq.IValueEnumerator<{{elementType}}>
        {
            public {{elementType}} Current => default!;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count){
                count=0;
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span){
                span = global::System.ReadOnlySpan<{{elementType}}>.Empty;
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination,global::System.Index offset) => true;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current){
                current=default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => string.Empty;
}

/*
RepeatRoot は Enumerable.Repeat(value, count) を値と残数だけで生成する。

生成例:
  if(index++ < count) {
      current = value;
      return true;
  }

バリエーション:
  Count/Any/Single など要素値を見ない終端は count 式だけで結果を出せる。
*/
internal sealed class RepeatRoot(int 番号,string valueExpression,string countExpression,string elementType):Enumerable(番号) {
    internal string ValueExpression => valueExpression;
    internal string CountExpression => countExpression;
    public override string? NonEnumeratedCountExpression => countExpression;
    public override bool CountPreserving => true;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Repeat<{elementType}>({valueExpression}, {countExpression})";
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,valueExpression,countExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"{elementType} value, int count";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}} < count){
            current = this.Current = value;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}}{{this.StructTypeParameters}} : global::Seamless.Linq.IValueEnumerator<{{elementType}}>
        {
            private {{elementType}} value;
            private int count;
            private int {{this.e}} = -1;
            public {{this.Struct}}({{elementType}} value,int count) { this.value = value; this.count = count; }
            public {{elementType}} Current{get;private set;}=default!;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count){
                count = this.count - ({{this.e}} + 1);
                return true;
            }
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination,global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current){
                if(++{{this.e}} < count){
                    current = this.Current = value;
                    return true;
                }
                current=default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => $$"""
        {
            var __value{{番号}}={{valueExpression}};
            var __count{{番号}}={{countExpression}};
            for(var __i{{番号}}=0;__i{{番号}}<__count{{番号}};__i{{番号}}++){
                {{consumerName}}.Consume(__value{{番号}});
            }
        }
        """;
}

internal sealed class InfiniteSequenceRoot(int 番号,string startExpression,string stepExpression,string elementType):Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.InfiniteSequence<{elementType}>({startExpression}, {stepExpression})";
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.Struct,startExpression,stepExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"{elementType} start, {elementType} step";
    public override string GetEnumeratorConstructor => $"ValidateNotNull(start, nameof(start));\nValidateNotNull(step, nameof(step));";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.hasValue}};
        private static void ValidateNotNull<T>(T value, string name){
            if(value is null){
                throw new global::System.ArgumentNullException(name);
            }
        }
        private static T Add<T>(T left, T right)
            where T : global::System.Numerics.IAdditionOperators<T, T, T>
            => left + right;
        """;
    public override string GetTryGetNextBody => $$"""
        if(!this.{{this.hasValue}}){
            this.{{this.hasValue}} = true;
            current = this.Current = start;
            return true;
        }
        start = Add<{{elementType}}>(start, step);
        current = this.Current = start;
        return true;
        """;
    public override string ElementType => elementType;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}

/*
SequenceRoot は start/end/step で表せる数列を生成する root。

生成例:
  if(step > 0 ? currentValue <= end : currentValue >= end) {
      current = currentValue;
      currentValue += step;
      return true;
  }

設計意図:
  RangeRoot より一般的な数列用。元 collection を持たず、境界判定と加算だけで TryGetNext を構成する。
*/
internal sealed class SequenceRoot(int 番号,string startExpression,string endInclusiveExpression,string stepExpression,string elementType):Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Sequence<{elementType}>({startExpression}, {endInclusiveExpression}, {stepExpression})";
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.Struct,startExpression,endInclusiveExpression,stepExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"{elementType} start, {elementType} endInclusive, {elementType} step";
    public override string GetEnumeratorConstructor => $$"""
        Validate<{{elementType}}>(start, endInclusive, step);
        this.{{this.state}} = IsIncrement<{{elementType}}>(step);
        """;
    public override string GetEnumeratorFields => $$"""
        private bool {{this.hasValue}};
        private readonly bool {{this.state}};
        private static void Validate<T>(T start, T endInclusive, T step)
            where T : global::System.Numerics.INumber<T> {
            if(start is null){
                throw new global::System.ArgumentNullException(nameof(start));
            }
            if(T.IsNaN(start)){
                throw new global::System.ArgumentOutOfRangeException(nameof(start));
            }
            if(endInclusive is null){
                throw new global::System.ArgumentNullException(nameof(endInclusive));
            }
            if(T.IsNaN(endInclusive)){
                throw new global::System.ArgumentOutOfRangeException(nameof(endInclusive));
            }
            if(step is null){
                throw new global::System.ArgumentNullException(nameof(step));
            }
            if(T.IsNaN(step)){
                throw new global::System.ArgumentOutOfRangeException(nameof(step));
            }
            if(T.IsZero(step)){
                if(start != endInclusive){
                    throw new global::System.ArgumentOutOfRangeException(nameof(step));
                }
                return;
            }
            if(T.IsPositive(step)){
                if(endInclusive < start){
                    throw new global::System.ArgumentOutOfRangeException(nameof(endInclusive));
                }
                return;
            }
            if(endInclusive > start){
                throw new global::System.ArgumentOutOfRangeException(nameof(endInclusive));
            }
        }
        private static bool IsIncrement<T>(T step)
            where T : global::System.Numerics.INumber<T>
            => T.IsZero(step) || T.IsPositive(step);
        private static T Add<T>(T left, T right)
            where T : global::System.Numerics.IAdditionOperators<T, T, T>
            => left + right;
        private static bool GreaterThanOrEqual<T>(T left, T right)
            where T : global::System.Numerics.IComparisonOperators<T, T, bool>
            => left >= right;
        private static bool LessThanOrEqual<T>(T left, T right)
            where T : global::System.Numerics.IComparisonOperators<T, T, bool>
            => left <= right;
        private static bool Equal<T>(T left, T right)
            where T : global::System.Numerics.IEqualityOperators<T, T, bool>
            => left == right;
        private static bool NotEqual<T>(T left, T right)
            where T : global::System.Numerics.IEqualityOperators<T, T, bool>
            => left != right;
        """;
    public override string GetTryGetNextBody => $$"""
        if(!this.{{this.hasValue}}){
            this.{{this.hasValue}} = true;
            current = this.Current = start;
            return true;
        }

        var next = Add<{{elementType}}>(this.Current, step);
        if(this.{{this.state}}){
            if(GreaterThanOrEqual<{{elementType}}>(next, endInclusive) || LessThanOrEqual<{{elementType}}>(next, this.Current)){
                if(Equal<{{elementType}}>(next, endInclusive) && NotEqual<{{elementType}}>(this.Current, next)){
                    current = this.Current = next;
                    return true;
                }
                current=default!;
                return false;
            }

            current = this.Current = next;
            return true;
        }

        if(LessThanOrEqual<{{elementType}}>(next, endInclusive) || GreaterThanOrEqual<{{elementType}}>(next, this.Current)){
            if(Equal<{{elementType}}>(next, endInclusive) && NotEqual<{{elementType}}>(this.Current, next)){
                current = this.Current = next;
                return true;
            }
            current=default!;
            return false;
        }

        current = this.Current = next;
        return true;
        """;
    public override string ElementType => elementType;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}

// Cast<T> の source は System.Collections.IEnumerable なので、generic Root とは別に object として流す。
internal class NonGenericRoot(int 番号,string sourceExpression):Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,sourceExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => "global::System.Collections.IEnumerable source";
    public override string GetEnumeratorConstructor => $"this.{this.e} = source.GetEnumerator();";
    public override string GetEnumeratorFields => $"private global::System.Collections.IEnumerator {this.e};";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()){
            current = this.Current = {{this.e}}.Current;
            return true;
        }
        current=default!;
        return false;
        """;
    protected override string GetEnumeratorDisposeBody => $$"""
        if({{this.e}} is global::System.IDisposable disposable){
            disposable.Dispose();
        }
        """;
    public override string ElementType => "object";
}
