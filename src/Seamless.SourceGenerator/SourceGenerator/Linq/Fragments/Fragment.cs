namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class Fragment(
    int 番号
) {
    private protected const string AggressiveInliningAttribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";

    // 生成コード内で enumerator 変数名として使う。
    private protected string e => $"e{番号}";
    // ループ本体の先頭ラベル名。
    private protected string Head=> $"Head{番号}";
    // MoveNext 判定へ戻る開始ラベル名。
    private protected string Start=> $"Start{番号}";
    // 条件不成立時に現在要素を捨てるラベル名。
    private protected string Skip=> $"Skip{番号}";
    // スカラー演算の早期終了先ラベル名。
    private protected string End=> $"End{番号}";
    // 集約結果や中間 List の変数名。
    protected string result=> $"result{番号}";
    // 補助 struct 名。
    private protected string Struct=> $"Struct{番号}";
    // Lookup/Dictionary 系演算の一時変数名。
    private protected string lookup=> $"lookup{番号}";
    // 現在要素のローカル名。
    private protected string item=> $"ite{番号}";
    // 多重評価を避けるための一時変数名。
    private protected string temp=> $"temp{番号}";
    // index オーバーロード用の添字変数名。
    private protected string a=> $"a{番号}";
    // GroupBy/Join 系のキー変数名。
    private protected string key=> $"key{番号}";
    // GroupBy 系のグループ変数名。
    private protected string group=> $"group{番号}";
    //private protected string seed=> $"seed{番号}";
    // Aggregate などで初回要素を読んだかどうかを表すフラグ名。
    private protected string hasValue => $"hasValue{番号}";
    // OrderBy 系の並び替え済みバッファ名。
    private protected string ordered => $"ordered{番号}";
    // Enumerator.Current を保持するためのフィールド名。
    private protected string currentValue => $"currentValue{番号}";
    // 複数段階の列挙状態を管理する state 名。
    private protected string state => $"state{番号}";
    // スカラー結果を 1 回だけ返したかどうかのフラグ名。
    private protected string yielded => $"yielded{番号}";
    // グループ列挙内の現在位置。
    private protected string groupIndex => $"groupIndex{番号}";
    // Join/Zip 系で外側要素を保持する変数名。
    private protected string outerValue => $"outerValue{番号}";
    // struct closure: 含有クラスの完全修飾型名。null なら @this 不使用。
    public string? ContainingTypeName { get; set; }

    // Enumerator struct 内ではメソッドローカルのキャプチャ変数を直接参照できない。
    // delegate は渡さず、必要なキャプチャ変数だけ primary constructor/static helper に渡して、
    // ラムダ本体は FuncAssign/FuncInvoke で式として展開する。
    private protected bool NeedsStructClosure(FuncFragment func) => func.CapturedLocals.Length > 0;

    private protected string ClosurePrimaryCtorSuffix(params FuncFragment[] funcs) =>
        ClosureParameterSuffix(funcs);

    private protected string ClosureMethodParameterSuffix(params FuncFragment[] funcs) =>
        ClosureParameterSuffix(funcs);

    private protected string ClosureArgsSuffix(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        return captures.Length == 0
            ? string.Empty
            : ", " + string.Join(", ", captures.Select(static capture => capture.Name));
    }

    private protected string ClosureFields(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        return captures.Length == 0
            ? string.Empty
            : string.Join("\n", captures.Select(static capture => $"private readonly {capture.TypeName} {capture.Name};"));
    }

    private protected string ClosureFieldAssignments(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        return captures.Length == 0
            ? string.Empty
            : string.Join("\n", captures.Select(static capture => $"this.{capture.Name} = {capture.Name};"));
    }

    private static string ClosureParameterSuffix(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        return captures.Length == 0
            ? string.Empty
            : ", " + string.Join(", ", captures.Select(static capture => $"{capture.TypeName} {capture.Name}"));
    }

    private static IEnumerable<(string Name, string TypeName)> DistinctCapturedLocals(params FuncFragment[] funcs) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in funcs) {
            foreach(var capture in func.CapturedLocals) {
                if(names.Add(capture.Name)) {
                    yield return capture;
                }
            }
        }
    }

    // ContainingTypeName をこの断片と全子断片に再帰的に伝播する。
    // リフレクションで Fragment 型フィールドを走査するため、個別の override は不要。
    public virtual void PropagateContainingTypeName(string? name) {
        ContainingTypeName = name;
        foreach(var field in EnumerateInstanceFields(GetType())) {
            if(typeof(Fragment).IsAssignableFrom(field.FieldType) && field.GetValue(this) is Fragment child) {
                child.PropagateContainingTypeName(name);
            }
            if(typeof(FuncFragment).IsAssignableFrom(field.FieldType)
                && field.GetValue(this) is InlinedScalarFunc inlinedScalarFunc) {
                inlinedScalarFunc.ScalarFragment.PropagateContainingTypeName(name);
            }
        }
    }

    // UnsafeAccessor コードを断片ツリー全体から収集する。
    // リフレクションで FuncFragment / Fragment 型フィールドを再帰的に走査する。
    public IEnumerable<string> CollectAllUnsafeAccessorCodes() {
        foreach(var field in EnumerateInstanceFields(GetType())) {
            if(typeof(FuncFragment).IsAssignableFrom(field.FieldType)
                && field.GetValue(this) is FuncFragment funcFrag) {
                // ラムダ本体内のフィールド/static メソッドアクセス用 UnsafeAccessor
                foreach(var code in funcFrag.BodyUnsafeAccessorCodes) {
                    yield return code;
                }
                if(funcFrag is InlinedScalarFunc inlinedScalarFunc) {
                    foreach(var code in inlinedScalarFunc.ScalarFragment.CollectAllUnsafeAccessorCodes()) {
                        yield return code;
                    }
                }
            }
            if(typeof(Fragment).IsAssignableFrom(field.FieldType)
                && field.GetValue(this) is Fragment child) {
                foreach(var code in child.CollectAllUnsafeAccessorCodes())
                    yield return code;
            }
        }
    }

    private static IEnumerable<FieldInfo> EnumerateInstanceFields(Type type) {
        for(var current = type; current is not null && current != typeof(object); current = current.BaseType) {
            foreach(var field in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
                yield return field;
            }
        }
    }

    // 補助 struct 群の宣言本体。不要なら空文字列を返す。
    public abstract string StructMethod { get; }
    // ValueEnumerable/IValueEnumerator 用の struct 定義本体。
    public abstract string GetEnumeratorStruct { get; }
    // TryGetNext の本体コード片。
    public abstract string GetTryGetNextBody { get; }
    // Current プロパティが返す式。
    public abstract string GetCurrentExpression { get; }
    // 生成した enumerator struct の型名。PrimarySource が設定されると自動でジェネリック型名を返す。
    public virtual string EnumeratorTypeName =>
        PrimarySource is { } src ? $"{Struct}<{src.EnumeratorTypeName}>" : Struct;
    // Enumerable を直接返す interceptor 用の new Enumerator(...) 式。
    public virtual string GetEnumeratorExpression => throw new NotSupportedException($"{GetType().Name} does not support Enumerator mode expression generation.");
    // primary constructor を使う enumerator struct の引数一覧。空なら通常 struct 宣言。
    public virtual string GetEnumeratorPrimaryConstructorParameters => string.Empty;
    // enumerator struct が持つ追加フィールド定義。
    public virtual string GetEnumeratorFields => string.Empty;
    // enumerator struct のコンストラクタ定義。
    public virtual string GetEnumeratorConstructor => string.Empty;
    // Enumerable 断片が List<T> へ結果を積む共通形。
    private protected static string var_List(string name,string ElementType) =>
        $"var {name}=new global::System.Collections.Generic.List<{ElementType}>();";
    private protected string FuncSupport(FuncFragment func,string methodName) =>
        func.SupportingMemberSources;

    // selector/predicate の呼び出しを、可能ならその場の式へ展開する。
    // キャプチャ変数付きラムダもインライン展開可能（キャプチャ変数はインターセプト先メソッドのスコープに存在する）。
    private protected string FuncInvoke(FuncFragment func,string methodName,params string[] arguments) {
        if(func.CanInlineAssign) {
            return func.RenderInlineExpression(arguments);
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // 代入文が必要なときの共通ヘルパー。
    private protected string FuncAssign(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.RequiresStatementInlining) {
            return func.RenderInlineAssign(destination,番号,arguments);
        }

        if(func.CanInlineAssign) {
            return func.RenderInlineAssign(destination,番号,arguments);
        }

        if(func is LambdaFunc) {
            throw LambdaInlineUnsupported(func,methodName);
        }
        return $"{destination}={FuncInvoke(func,methodName,arguments)};";
    }

    // フラットループ内では全ラムダを直接式へ展開する。
    private protected string FuncAssignFlat(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.RequiresStatementInlining) {
            return func.RenderInlineAssign(destination,番号,arguments);
        }

        return $"{destination}={FuncInvokeFlat(func,methodName,arguments)};";
    }

    // 各断片が持つ Current/TryGetNext/Dispose を IValueEnumerator struct にまとめる。
    private protected string DefineEnumeratorStructCore(string currentType,string disposeBody) {
        return $$"""
            {{GetEnumeratorStructDeclaration($"global::Seamless.Linq.Carrier.IValueEnumerator<{currentType}>, global::System.Collections.Generic.IEnumerator<{currentType}>")}}
                {{GetEnumeratorFields}}
                {{GetEnumeratorConstructor}}
                public {{currentType}} Current => {{GetCurrentExpression}};
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
                public bool TryGetSpan(out global::System.ReadOnlySpan<{{currentType}}> span) {
                    span = default;
                    return false;
                }
                {{AggressiveInliningAttribute}}
                public bool TryCopyTo(scoped global::System.Span<{{currentType}}> destination, global::System.Index offset) => false;
                {{AggressiveInliningAttribute}}
                public bool TryGetNext(out {{currentType}} current) {
                    {{GetTryGetNextBody}}
                }
                {{AggressiveInliningAttribute}}
                public void Dispose() {
                    {{disposeBody}}
                }
            }
            """;
    }

    // 単一上流源を持つ断片はこれをオーバーライドする。
    // 設定すると StructTypeParameters/StructTypeConstraints/EnumeratorTypeName が自動導出される。
    protected virtual Enumerable? PrimarySource => null;

    // 生成 struct に付ける型パラメータ（"<TEnum>" 等）。PrimarySource が設定されると "<TEnum>" になる。
    protected virtual string StructTypeParameters => PrimarySource is not null ? "<TEnum>" : string.Empty;
    // 生成 struct に付ける where 制約。PrimarySource が設定されると struct 制約付きの where 句になる。
    protected virtual string StructTypeConstraints =>
        PrimarySource is { } src
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{src.ElementType}>"
            : string.Empty;

    private protected string GetEnumeratorStructDeclaration(string interfaceType) {
        var tp = StructTypeParameters;
        var tc = string.IsNullOrEmpty(StructTypeConstraints) ? "" : $"\n    {StructTypeConstraints}";
        return string.IsNullOrWhiteSpace(GetEnumeratorPrimaryConstructorParameters)
            ? $"private struct {Struct}{tp} : {interfaceType}{tc} {{"
            : $"private struct {Struct}{tp}({GetEnumeratorPrimaryConstructorParameters}) : {interfaceType}{tc} {{";
    }

    private protected string GetGenericEnumeratorStructDeclaration(string typeParameters,string interfaceType) {
        var tc = string.IsNullOrEmpty(StructTypeConstraints) ? "" : $"\n    {StructTypeConstraints}";
        return string.IsNullOrWhiteSpace(GetEnumeratorPrimaryConstructorParameters)
            ? $"private struct {Struct}{typeParameters} : {interfaceType}{tc} {{"
            : $"private struct {Struct}{typeParameters}({GetEnumeratorPrimaryConstructorParameters}) : {interfaceType}{tc} {{";
    }

    // struct の TryGetNext 内で使う。static ラムダまたはキャプチャ変数付きラムダならインライン展開する。
    private protected string FuncInvokeField(FuncFragment func,string methodName,params string[] arguments) {
        if(func.CanInlineAssign) {
            return func.RenderInlineExpression(arguments);
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // フラットループ内で使う。全ラムダをインライン展開（@this が静的メソッドスコープで有効）。
    private protected string FuncInvokeFlat(FuncFragment func,string methodName,params string[] arguments) {
        if(!func.CanInlineAssign) {
            throw LambdaInlineUnsupported(func,methodName);
        }
        return func.RenderInlineExpression(arguments);
    }

    // terminal チェーンでフラットループ生成をサポートするかどうか。
    public virtual bool SupportsFlatLoop => false;

    // フラットループ生成。elementVariable に各要素を束縛し bodyCode を実行するコードを返す。
    // 末尾メソッドのスコープに展開されるため @this が使用可能。
    public virtual string GetFlatLoopCode(string elementVariable,string bodyCode) =>
        throw new NotSupportedException($"{GetType().Name} does not support flat loop code generation.");

    // IEnumerable<T> / T[] の文字列型名から要素型名を取り出す。
    private protected static string SequenceElementTypeName(string sequenceTypeName) {
        if(sequenceTypeName.EndsWith("[]",StringComparison.Ordinal)) {
            return sequenceTypeName[..^2];
        }

        const string enumerablePrefix = "global::System.Collections.Generic.IEnumerable<";
        if(sequenceTypeName.StartsWith(enumerablePrefix,StringComparison.Ordinal)
            && sequenceTypeName.EndsWith(">",StringComparison.Ordinal)) {
            return sequenceTypeName[enumerablePrefix.Length..^1];
        }

        throw new InvalidOperationException($"Could not determine sequence element type from '{sequenceTypeName}'.");
    }

    // これらのメソッドは struct closure が不要になったため削除された。
    // 代わりに FuncInvokeField/FuncAssign を直接使用する。

    // FuncFragment から index 番目の引数型名を取り出す。
    private protected static string FuncParameterTypeName(FuncFragment func,int index) {
        if(func.ParameterTypeNames.Length>index&&!string.IsNullOrEmpty(func.ParameterTypeNames[index])) {
            return func.ParameterTypeNames[index];
        }
        throw new InvalidOperationException($"FuncFragment parameter type {index} is not available.");
    }

    // FuncFragment の戻り値型名を取り出す。
    private protected static string FuncReturnTypeName(FuncFragment func) {
        return func.ReturnTypeName;
    }

    private protected static InvalidOperationException LambdaInlineUnsupported(FuncFragment func,string methodName) =>
        new($"ラムダ式をインライン展開できませんでした。'{methodName}' をデリゲート Invoke 呼び出しへ逃がすことは許可されていません。Body: '{func.InlineBody}'");

    internal static string JoinStaticMethods(string left, string right) =>
        string.Join("\n", new[] { left, right }.Where(static s => !string.IsNullOrWhiteSpace(s)));
}

internal delegate bool BuildEnumerableFragmentDelegate(
    SemanticModel semanticModel,
    ExpressionSyntax source,
    INamedTypeSymbol containingType,
    ref int uniqueNumber,
    out Enumerable fragment,
    out string staticMethods,
    List<string>? collector);

internal delegate bool BuildFuncFragmentDelegate(
    ExpressionSyntax expression,
    ITypeSymbol delegateType,
    ref int uniqueNumber,
    out FuncFragment fragment,
    SemanticModel? semanticModel,
    INamedTypeSymbol? containingType);

internal delegate Fragment? CreateFragmentDelegate(
    string methodName,
    string variantName,
    int number,
    object?[] extraArgs);

internal delegate bool TryGetLambdaBodyDelegate(
    LambdaExpressionSyntax lambda,
    out ExpressionSyntax body);

internal delegate List<string> GetLambdaParametersDelegate(
    LambdaExpressionSyntax lambda);

internal delegate bool BuildInvocationFragmentDelegate(
    SemanticModel semanticModel,
    InvocationExpressionSyntax invocation,
    INamedTypeSymbol containingType,
    ref int uniqueNumber,
    out Fragment fragment,
    out string staticMethods,
    List<string>? unsafeAccessorCollector);
