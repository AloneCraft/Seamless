namespace Seamless.SourceGenerator.Linq.Fragments;

// Root は他の Fragment へ分解されない列挙ソースそのものを表す。
// enumeratorTypeName には具象 GetEnumerator() 戻り値型名を渡す。
// IEnumerable<T>/配列の場合は IEnumerator<T>、List<T> 等の具象型の場合は List<T>.Enumerator 等の値型エニュメレータ名になる。
// 構造体 Struct1<TEnum> は TEnum でパラメータ化され、呼び出し側で具体型引数により単相化される。
public class Root(int 番号, string sourceExpression, string elementType, string enumeratorTypeName, bool useEnumerableInterface) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    // 型引数を埋め込んだ完全な struct 名（呼び出し側では具象型引数を持つ）。
    public override string EnumeratorTypeName => $"{Struct}<{enumeratorTypeName}>";
    public override string GetEnumeratorExpression {
        get {
            var sourceExpr = useEnumerableInterface
                ? $"((global::System.Collections.Generic.IEnumerable<{elementType}>)({sourceExpression}))"
                : sourceExpression;
            return $"new {EnumeratorTypeName}({sourceExpr}.GetEnumerator())";
        }
    }
    // TEnum でパラメータ化された struct を手動で出力する（where 制約が struct 宣言の opening brace より前に来る必要があるため）。
    public override string GetEnumeratorStruct => $$"""
        private struct {{Struct}}<TEnum>(TEnum {{e}}) : global::Seamless.Linq.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
            where TEnum : {{(useEnumerableInterface ? "" : "struct, ")}}global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            public {{elementType}} Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) {
                span = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination, global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current) {
                if({{e}}.MoveNext()) {
                    current = this.Current = {{e}}.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() {
                {{e}}.Dispose();
            }
        }
        """;
    // GetEnumeratorStruct を手動出力しているのでこれらは使われないが実装が必要。
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            current = this.Current = {{e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => elementType;
}

// 配列ソース専用。インデックスベースで走査し、IEnumerator<T> を一切介さない完全具象 struct を生成する。
// TryGetSpan / TryGetNonEnumeratedCount も正しく実装するため GetEnumeratorStruct を手動出力する。
public class ArrayRoot(int 番号, string sourceExpression, string elementType) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    // PrimarySource == null なので基底の EnumeratorTypeName は Struct のみ（型パラメータなし）を返す。
    public override string GetEnumeratorExpression => $"new {Struct}({sourceExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"{elementType}[] source";
    public override string GetEnumeratorFields => $"private int {e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{e}} < source.Length) {
            current = source[{{e}}];
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => $"source[{e}]";
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{Struct}}({{elementType}}[] source) : global::Seamless.Linq.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private int {{e}} = -1;
            public {{elementType}} Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - ({{e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) {
                span = global::System.MemoryExtensions.AsSpan(source, {{e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, {{e}} + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current) {
                if(++{{e}} < source.Length) {
                    current = this.Current = source[{{e}}];
                    return true;
                }
                current = default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() { }
        }
        """;
}

// List<T> ソース専用。List.Enumerator を包む素通し Root + バージョンチェックを避け、
// CollectionsMarshal.AsSpan(list) で内部配列を直接 Span として走査する（配列ソースと同等の高速パス）。
// 内部フィールド名に依存せず public API のみを使うため安全。TryGetSpan/TryGetNonEnumeratedCount/TryCopyTo も提供する。
public class ListRoot(int 番号, string sourceExpression, string elementType) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    // PrimarySource == null なので基底の EnumeratorTypeName は Struct のみ（型パラメータなし）を返す。
    public override string GetEnumeratorExpression => $"new {Struct}({sourceExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.List<{elementType}> source";
    public override string GetEnumeratorFields => $"private int {e} = -1;";
    public override string GetTryGetNextBody => $$"""
        var {{temp}} = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source);
        if(++{{e}} < {{temp}}.Length) {
            current = {{temp}}[{{e}}];
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => "Current";
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{Struct}}(global::System.Collections.Generic.List<{{elementType}}> source) : global::Seamless.Linq.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private int {{e}} = -1;
            public {{elementType}} Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Count - ({{e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) {
                span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source).Slice({{e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination, global::System.Index offset) {
                var remaining = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source).Slice({{e}} + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current) {
                var {{temp}} = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source);
                if(++{{e}} < {{temp}}.Length) {
                    current = this.Current = {{temp}}[{{e}}];
                    return true;
                }
                current = default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() { }
        }
        """;
}

// IEnumerator<T> を実装しない duck-typing struct エニュメレータ（例: ImmutableArray<T>.Enumerator）向け。
// 具象エニュメレータ型を直接フィールドとして持つ非ジェネリック struct を生成する（直接展開）。
// TEnum の struct 制約が不要なため CS0315 が発生しない。
// Dispose() は no-op。duck-typing で IDisposable も実装するなら IEnumerator<T> も実装するはずで、
// その場合は通常の Root パスで処理されるため、ここには到達しない。
public class DuckTypingRoot(int 番号, string sourceExpression, string elementType, string concreteEnumeratorType) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string EnumeratorTypeName => Struct;
    public override string GetEnumeratorExpression => $"new {Struct}({sourceExpression}.GetEnumerator())";
    public override string GetEnumeratorPrimaryConstructorParameters => $"{concreteEnumeratorType} {e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            current = this.Current = {{e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{Struct}}({{concreteEnumeratorType}} {{e}}) : global::Seamless.Linq.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            public {{elementType}} Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) {
                span = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination, global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current) {
                if({{e}}.MoveNext()) {
                    current = this.Current = {{e}}.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() { }
        }
        """;
}

// Cast<T> の source は System.Collections.IEnumerable なので、generic Root とは別に object として流す。
public class NonGenericRoot(int 番号, string sourceExpression) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({sourceExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => "global::System.Collections.IEnumerable source";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.IEnumerator {{e}} = source.GetEnumerator();
        """;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            current = this.Current = {{e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $$"""
        if({{e}} is global::System.IDisposable disposable) {
            disposable.Dispose();
        }
        """;
    public override string ElementType => "object";
}
