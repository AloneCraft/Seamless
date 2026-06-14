namespace Tsavorite.SourceGenerator.Linq.Fragments;

// Root は他の Fragment へ分解されない列挙ソースそのものを表す。
// enumeratorTypeName には具象 GetEnumerator() 戻り値型名を渡す。
// IEnumerable<T>/配列の場合は IEnumerator<T>、List<T> 等の具象型の場合は List<T>.Enumerator 等の値型エニュメレータ名になる。
// 構造体 Struct1<TEnum> は TEnum でパラメータ化され、呼び出し側で具体型引数により単相化される。
public class Root(int 番号, string sourceExpression, string elementType, string enumeratorTypeName, bool useEnumerableInterface) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    // 型引数を埋め込んだ完全な struct 名（呼び出し側では具象型引数を持つ）。
    public override string EnumeratorTypeName => $"{this.Struct}<{enumeratorTypeName}>";
    public override string? DirectEnumerableExpression => useEnumerableInterface
        ? $"((global::System.Collections.Generic.IEnumerable<{elementType}>)({sourceExpression}))"
        : null;
    public override string GetEnumeratorExpression {
        get {
            var sourceExpr = useEnumerableInterface
                ? $"((global::System.Collections.Generic.IEnumerable<{elementType}>)({sourceExpression}))"
                : sourceExpression;
            return $"new {this.EnumeratorTypeName}({sourceExpr}.GetEnumerator())";
        }
    }
    // TEnum でパラメータ化された struct を手動で出力する（where 制約が struct 宣言の opening brace より前に来る必要があるため）。
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}}<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
            where TEnum : {{(useEnumerableInterface ? "" : "struct, ")}}global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private TEnum {{this.e}};
            public {{this.Struct}}(TEnum {{this.e}}) { this.{{this.e}} = {{this.e}}; }
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
                if({{this.e}}.MoveNext()) {
                    current = this.Current = {{this.e}}.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() {
                {{this.e}}.Dispose();
            }
        }
        """;
    // GetEnumeratorStruct を手動出力しているのでこれらは使われないが実装が必要。
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            current = this.Current = {{this.e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => elementType;
}

// 配列ソース専用。インデックスベースで走査し、IEnumerator<T> を一切介さない完全具象 struct を生成する。
// TryGetSpan / TryGetNonEnumeratedCount も正しく実装するため GetEnumeratorStruct を手動出力する。
public class ArrayRoot(int 番号, string sourceExpression, string elementType) : Enumerable(番号) {
    public override bool CanProvideSpan => true;
    public override string StructMethod => string.Empty;
    // PrimarySource == null なので基底の EnumeratorTypeName は Struct のみ（型パラメータなし）を返す。
    public override string GetEnumeratorExpression => $"new {this.Struct}({sourceExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"{elementType}[] source";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}} < source.Length) {
            current = source[{{this.e}}];
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => $"source[{this.e}]";
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Tsavorite.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private {{elementType}}[] source;
            private int {{this.e}} = -1;
            public {{this.Struct}}({{elementType}}[] source) { this.source = source; }
            public {{elementType}} Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - ({{this.e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) {
                span = global::System.MemoryExtensions.AsSpan(source, {{this.e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, {{this.e}} + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current) {
                if(++{{this.e}} < source.Length) {
                    current = this.Current = source[{{this.e}}];
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
    public override bool CanProvideSpan => true;
    public override string StructMethod => string.Empty;
    // PrimarySource == null なので基底の EnumeratorTypeName は Struct のみ（型パラメータなし）を返す。
    public override string GetEnumeratorExpression => $"new {this.Struct}({sourceExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.List<{elementType}> source";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        var {{this.temp}} = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source);
        if(++{{this.e}} < {{this.temp}}.Length) {
            current = {{this.temp}}[{{this.e}}];
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => "Current";
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Tsavorite.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private global::System.Collections.Generic.List<{{elementType}}> source;
            private int {{this.e}} = -1;
            public {{this.Struct}}(global::System.Collections.Generic.List<{{elementType}}> source) { this.source = source; }
            public {{elementType}} Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Count - ({{this.e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) {
                span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source).Slice({{this.e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination, global::System.Index offset) {
                var remaining = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source).Slice({{this.e}} + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current) {
                var {{this.temp}} = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(source);
                if(++{{this.e}} < {{this.temp}}.Length) {
                    current = this.Current = {{this.temp}}[{{this.e}}];
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
    public override string EnumeratorTypeName => this.Struct;
    public override string GetEnumeratorExpression => $"new {this.Struct}({sourceExpression}.GetEnumerator())";
    public override string GetEnumeratorPrimaryConstructorParameters => $"{concreteEnumeratorType} {this.e}";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            current = this.Current = {{this.e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Tsavorite.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private {{concreteEnumeratorType}} {{this.e}};
            public {{this.Struct}}({{concreteEnumeratorType}} {{this.e}}) { this.{{this.e}} = {{this.e}}; }
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
                if({{this.e}}.MoveNext()) {
                    current = this.Current = {{this.e}}.Current;
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

public sealed class RangeRoot(int 番号,string startExpression,string countExpression) : Enumerable(番号) {
    internal string StartExpression => startExpression;
    internal string CountExpression => countExpression;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Range({startExpression}, {countExpression})";
    public override string GetEnumeratorExpression => $"new {this.Struct}({startExpression}, {countExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => "int start, int count";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}} < count) {
            current = this.Current = start + {{this.e}};
            return true;
        }
        current = default;
        return false;
        """;
    public override string GetCurrentExpression => "Current";
    public override string ElementType => "int";
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int start;
            private int count;
            private int {{this.e}} = -1;
            public {{this.Struct}}(int start,int count) { this.start = start; this.count = count; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - ({{this.e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out int current) {
                if(++{{this.e}} < count) {
                    current = this.Current = start + {{this.e}};
                    return true;
                }
                current = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() { }
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => $$"""
        {
            var __start{{番号}}={{startExpression}};
            var __count{{番号}}={{countExpression}};
            for(var __i{{番号}}=0;__i{{番号}}<__count{{番号}};__i{{番号}}++) {
                {{consumerName}}.Consume(__start{{番号}} + __i{{番号}});
            }
        }
        """;
}

public sealed class DescendingRangeRoot(int 番号,string startExpression,string countExpression) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Range({startExpression}, {countExpression}).Reverse()";
    public override string GetEnumeratorExpression => $"new {this.Struct}({startExpression}, {countExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => "int start, int count";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}} < count) {
            current = this.Current = start + count - 1 - {{this.e}};
            return true;
        }
        current = default;
        return false;
        """;
    public override string GetCurrentExpression => "Current";
    public override string ElementType => "int";
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int start;
            private int count;
            private int {{this.e}} = -1;
            public {{this.Struct}}(int start,int count) { this.start = start; this.count = count; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - ({{this.e}} + 1);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out int current) {
                if(++{{this.e}} < count) {
                    current = this.Current = start + count - 1 - {{this.e}};
                    return true;
                }
                current = default;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() { }
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => $$"""
        {
            var __start{{番号}}={{startExpression}};
            var __count{{番号}}={{countExpression}};
            for(var __i{{番号}}=__count{{番号}}-1;__i{{番号}}>=0;__i{{番号}}--) {
                {{consumerName}}.Consume(__start{{番号}} + __i{{番号}});
            }
        }
        """;
}

public sealed class EmptyRoot(int 番号,string elementType) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Empty<{elementType}>()";
    public override string GetEnumeratorExpression => $"new {this.Struct}()";
    public override string GetEnumeratorPrimaryConstructorParameters => string.Empty;
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => $$"""
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => "default!";
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Tsavorite.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            public {{elementType}} Current => default!;
            object? global::System.Collections.IEnumerator.Current => default;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => false;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span) {
                span = global::System.ReadOnlySpan<{{elementType}}>.Empty;
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination, global::System.Index offset) => true;
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current) {
                current = default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() { }
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => string.Empty;
}

public sealed class RepeatRoot(int 番号,string valueExpression,string countExpression,string elementType) : Enumerable(番号) {
    internal string ValueExpression => valueExpression;
    internal string CountExpression => countExpression;
    public override string StructMethod => string.Empty;
    public override string? DirectEnumerableExpression => $"global::System.Linq.Enumerable.Repeat<{elementType}>({valueExpression}, {countExpression})";
    public override string GetEnumeratorExpression => $"new {this.Struct}({valueExpression}, {countExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"{elementType} value, int count";
    public override string GetEnumeratorFields => $"private int {this.e} = -1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}} < count) {
            current = this.Current = value;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => "Current";
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Tsavorite.Carrier.IValueEnumerator<{{elementType}}>, global::System.Collections.Generic.IEnumerator<{{elementType}}>
        {
            private {{elementType}} value;
            private int count;
            private int {{this.e}} = -1;
            public {{this.Struct}}({{elementType}} value,int count) { this.value = value; this.count = count; }
            public {{elementType}} Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            {{AggressiveInliningAttribute}}
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            {{AggressiveInliningAttribute}}
            public bool MoveNext() => TryGetNext(out _);
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - ({{this.e}} + 1);
                return true;
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
                if(++{{this.e}} < count) {
                    current = this.Current = value;
                    return true;
                }
                current = default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose() { }
        }
        """;
    public override string GetConsumeExecutionStatement(string consumerName) => $$"""
        {
            var __value{{番号}}={{valueExpression}};
            var __count{{番号}}={{countExpression}};
            for(var __i{{番号}}=0;__i{{番号}}<__count{{番号}};__i{{番号}}++) {
                {{consumerName}}.Consume(__value{{番号}});
            }
        }
        """;
}

// Cast<T> の source は System.Collections.IEnumerable なので、generic Root とは別に object として流す。
public class NonGenericRoot(int 番号, string sourceExpression) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({sourceExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => "global::System.Collections.IEnumerable source";
    public override string GetEnumeratorConstructor => $"this.{this.e} = source.GetEnumerator();";
    public override string GetEnumeratorFields => $"private global::System.Collections.IEnumerator {this.e};";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            current = this.Current = {{this.e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $$"""
        if({{this.e}} is global::System.IDisposable disposable) {
            disposable.Dispose();
        }
        """;
    public override string ElementType => "object";
}
