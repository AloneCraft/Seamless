namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class Fragment(
    int 番号
) {
    private protected const string AggressiveInliningAttribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";

    // 生成コード内で enumerator 変数名として使う。
    private protected string e => $"__e{番号}";
    // ループ本体の先頭ラベル名。
    private protected string Head=> $"Head{番号}";
    // MoveNext 判定へ戻る開始ラベル名。
    private protected string Start=> $"Start{番号}";
    // 条件不成立時に現在要素を捨てるラベル名。
    private protected string Skip=> $"Skip{番号}";
    // スカラー演算の早期終了先ラベル名。
    private protected string End=> $"End{番号}";
    // 集約結果や中間 List の変数名。
    protected string result=> $"__r{番号}";
    // 補助 struct 名。
    private protected string Struct=> $"Struct{番号}";
    // Lookup/Dictionary 系演算の一時変数名。
    private protected string lookup=> $"__lk{番号}";
    // 現在要素のローカル名。
    private protected string item=> $"__it{番号}";
    // 多重評価を避けるための一時変数名。
    private protected string temp=> $"__tmp{番号}";
    // index オーバーロード用の添字変数名。
    private protected string a=> $"__a{番号}";
    // GroupBy/Join 系のキー変数名。
    private protected string key=> $"__k{番号}";
    // GroupBy 系のグループ変数名。
    private protected string group=> $"__g{番号}";
    //private protected string seed=> $"seed{番号}";
    // Aggregate などで初回要素を読んだかどうかを表すフラグ名。
    private protected string hasValue => $"__hv{番号}";
    // OrderBy 系の並び替え済みバッファ名。
    private protected string ordered => $"__ord{番号}";
    // Enumerator.Current を保持するためのフィールド名。
    private protected string currentValue => $"__cv{番号}";
    // 複数段階の列挙状態を管理する state 名。
    private protected string state => $"__st{番号}";
    // スカラー結果を 1 回だけ返したかどうかのフラグ名。
    private protected string yielded => $"__yd{番号}";
    // グループ列挙内の現在位置。
    private protected string groupIndex => $"__gi{番号}";
    // Join/Zip 系で外側要素を保持する変数名。
    private protected string outerValue => $"__ov{番号}";
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
        var delegates = DistinctDelegateFuncs(funcs).ToArray();
        var parts = captures.Select(static c => c.Name)
            .Concat(delegates.Select(static f => f.DelegateConstructorArg!));
        var joined = string.Join(", ", parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : ", " + joined;
    }

    private protected string ClosureFields(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        var delegates = DistinctDelegateFuncs(funcs).ToArray();
        var lines = captures.Select(static c => $"private readonly {c.TypeName} {c.Name};")
            .Concat(delegates.Select(static f => $"private readonly {f.DelegateTypeName} {f.DelegateFieldName};"));
        var joined = string.Join("\n", lines);
        return string.IsNullOrEmpty(joined) ? string.Empty : joined;
    }

    private protected string ClosureFieldAssignments(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        var delegates = DistinctDelegateFuncs(funcs).ToArray();
        var lines = captures.Select(static c => $"this.{c.Name} = {c.Name};")
            .Concat(delegates.Select(static f => $"this.{f.DelegateFieldName} = {f.DelegateFieldName};"));
        var joined = string.Join("\n", lines);
        return string.IsNullOrEmpty(joined) ? string.Empty : joined;
    }

    private static string ClosureParameterSuffix(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        var delegates = DistinctDelegateFuncs(funcs).ToArray();
        var parts = captures.Select(static c => $"{c.TypeName} {c.Name}")
            .Concat(delegates.Select(static f => $"{f.DelegateTypeName} {f.DelegateFieldName}"));
        var joined = string.Join(", ", parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : ", " + joined;
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

    private static IEnumerable<FuncFragment> DistinctDelegateFuncs(params FuncFragment[] funcs) {
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in funcs) {
            if(func.CanInvokeAsDelegate && func.DelegateFieldName is { } name && fieldNames.Add(name)) {
                yield return func;
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
        if(func.CanInvokeAsDelegate) {
            // primary constructor パラメータは this. なしでアクセスする（struct では this.param は無効）
            return $"{func.DelegateFieldName}.Invoke({string.Join(", ", arguments)})";
        }
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
        if(func.CanInvokeAsDelegate) {
            return $"{destination}={func.DelegateFieldName}.Invoke({string.Join(", ", arguments)});";
        }
        if(func.CanInlineAssign) {
            return func.RenderInlineAssign(destination,番号,arguments);
        }
        if(func is LambdaFunc) {
            throw LambdaInlineUnsupported(func,methodName);
        }
        return $"{destination}={FuncInvoke(func,methodName,arguments)};";
    }

    // TryGetNext 内で destination フィールドと out current の両方を設定する複合代入を生成する。
    // 単純ラムダなら current = destination = expr; の一行に。
    // RequiresStatementInlining またはデリゲート呼び出しの場合は FuncAssign の後に current = destination; を付加する。
    private protected string FuncAssignCurrent(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.RequiresStatementInlining || (!func.CanInlineAssign && !func.CanInvokeAsDelegate)) {
            return $"{FuncAssign(func,methodName,destination,arguments)}\ncurrent = {destination};";
        }
        var expr = FuncInvoke(func,methodName,arguments);
        return $"current = {destination} = {expr};";
    }

    // 述語評価専用ヘルパー。if文の条件式として使う場合に特化。
    // 単純ラムダなら直接式として展開（一時変数なし）。
    // RequiresStatementInlining=true（InlinedScalarFuncなど）なら一時変数経由にフォールバック。
    private protected (string preCode, string conditionExpr) FuncInvokeCondition(FuncFragment func,string methodName,string tempVar,params string[] arguments) {
        if(func.RequiresStatementInlining) {
            // InlinedScalarFunc など複数文が必要な場合は一時変数経由
            var declaration = $"{func.ReturnTypeName} {tempVar} = default!;";
            var assignStatement = func.RenderInlineAssign(tempVar,番号,arguments);
            return ($"{declaration}\n{assignStatement}", tempVar);
        }
        if(func.CanInvokeAsDelegate) {
            return (string.Empty, $"{func.DelegateFieldName}.Invoke({string.Join(", ", arguments)})");
        }
        if(func.CanInlineAssign) {
            // 単純ラムダなら直接式として展開
            return (string.Empty, func.RenderInlineExpression(arguments));
        }
        throw LambdaInlineUnsupported(func,methodName);
    }

    // 各断片が持つ Current/TryGetNext/Dispose を IValueEnumerator struct にまとめる。
    private protected string DefineEnumeratorStructCore(string currentType,string disposeBody) {
        var tryGetNextBody = IndentLines(GetTryGetNextBody, "        ");
        var dispose = IndentLines(disposeBody, "        ");
        var fields = IndentLines(GetEnumeratorFields, "    ");
        var ctor = IndentLines(GetEnumeratorConstructor, "    ");

        return $$"""
            {{GetEnumeratorStructDeclaration($"global::Seamless.Linq.Carrier.IValueEnumerator<{currentType}>, global::System.Collections.Generic.IEnumerator<{currentType}>")}}
            {{fields}}
            {{ctor}}
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
            {{tryGetNextBody}}
                }
                {{AggressiveInliningAttribute}}
                public void Dispose() {
            {{dispose}}
                }
            }
            """;
    }

    private protected static string IndentLines(string text, string indent) {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var result = new System.Text.StringBuilder();

        for (int i = 0; i < lines.Length; i++) {
            var line = lines[i];
            if (!string.IsNullOrWhiteSpace(line)) {
                result.Append(indent).Append(line);
            }
            if (i < lines.Length - 1) {
                result.AppendLine();
            }
        }

        return result.ToString();
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

    // 型推論用ファクトリメソッドの名前。
    // "_Factory" サフィックスは 静的メソッド群と本体を補助クラスへ退避21() の Struct{番号} リネーム正規表現に対応している。
    // 例: Struct0_Factory → {prefix}Struct0_Factory （メソッドごとにユニークな名前に変換される）。
    private protected string FactoryMethodName => $"{Struct}_Factory";

    // ファクトリメソッド本体のコンストラクタ呼び出し引数サフィックス。
    // ClosureArgsSuffix と異なり、DelegateConstructorArg（ラムダ式）ではなく DelegateFieldName（フィールド名）を使う。
    private protected string ClosureArgsSuffixForFactory(params FuncFragment[] funcs) {
        var captures = DistinctCapturedLocals(funcs).ToArray();
        var delegates = DistinctDelegateFuncs(funcs).ToArray();
        var parts = captures.Select(static c => c.Name)
            .Concat(delegates.Select(static f => f.DelegateFieldName!));
        var joined = string.Join(", ", parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : ", " + joined;
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
