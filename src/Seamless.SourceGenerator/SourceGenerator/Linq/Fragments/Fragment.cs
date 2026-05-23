using System;

namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class Fragment(
    int 番号
) {
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
    private protected string index=> $"index{番号}";
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

    // struct closure が必要かどうか判定。
    // @this（ContainingTypeName）またはキャプチャローカル（CapturedLocals）を primary ctor に渡してインライン展開する場合に true。
    private protected bool NeedsStructClosure(FuncFragment func) =>
        func.CanInlineAssign && !func.CanGenerateMethod &&
        (ContainingTypeName is not null || func.CapturedLocals.Length > 0);

    // struct primary ctor の closure パラメータ部分（@this と captured locals）を生成する。
    private protected string ClosurePrimaryCtorSuffix(FuncFragment func) {
        var parts = new List<string>();
        if(ContainingTypeName is not null) {
            parts.Add($"{ContainingTypeName} @this");
        }
        foreach(var (name,typeName) in func.CapturedLocals) {
            parts.Add($"{typeName} {name}");
        }
        return string.Join(", ",parts);
    }

    // new Struct(...) の closure 引数部分（@this と captured locals）を生成する。
    private protected string ClosureArgsSuffix(FuncFragment func) {
        var parts = new List<string>();
        if(ContainingTypeName is not null) {
            parts.Add("@this");
        }
        foreach(var (name,_) in func.CapturedLocals) {
            parts.Add(name);
        }
        return string.Join(", ",parts);
    }

    // ContainingTypeName をこの断片と全子断片に再帰的に伝播する。
    // リフレクションで Fragment 型フィールドを走査するため、個別の override は不要。
    public virtual void PropagateContainingTypeName(string? name) {
        ContainingTypeName = name;
        foreach(var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
            if(typeof(Fragment).IsAssignableFrom(field.FieldType) && field.GetValue(this) is Fragment child) {
                child.PropagateContainingTypeName(name);
            }
        }
    }

    // UnsafeAccessor コードを断片ツリー全体から収集する。
    // リフレクションで FuncFragment / Fragment 型フィールドを再帰的に走査する。
    public IEnumerable<string> CollectAllUnsafeAccessorCodes() {
        foreach(var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
            if(typeof(FuncFragment).IsAssignableFrom(field.FieldType)
                && field.GetValue(this) is ParameterFunc paramFunc
                && !string.IsNullOrEmpty(paramFunc.UnsafeAccessorCode)) {
                yield return paramFunc.UnsafeAccessorCode;
            }
            if(typeof(Fragment).IsAssignableFrom(field.FieldType)
                && field.GetValue(this) is Fragment child) {
                foreach(var code in child.CollectAllUnsafeAccessorCodes())
                    yield return code;
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
    // 生成した enumerator struct の型名。
    public string EnumeratorTypeName => Struct;
    // Enumerable を直接返す interceptor 用の new Enumerator(...) 式。
    public virtual string GetEnumeratorExpression => throw new NotSupportedException($"{this.GetType().Name} does not support Enumerator mode expression generation.");
    // primary constructor を使う enumerator struct の引数一覧。空なら通常 struct 宣言。
    public virtual string GetEnumeratorPrimaryConstructorParameters => string.Empty;
    // enumerator struct が持つ追加フィールド定義。
    public virtual string GetEnumeratorFields => string.Empty;
    // enumerator struct のコンストラクタ定義。
    public virtual string GetEnumeratorConstructor => string.Empty;
    // Enumerable 断片が List<T> へ結果を積む共通形。
    private protected static string var_List(string name,string ElementType) =>
        $"var {name}=new global::System.Collections.Generic.List<{ElementType}>();";
    // 断片ごとの関数補助 struct 名を安定して作る。
    private protected string FuncStructName(string methodName) => $"{Struct}_{methodName}";
    // LambdaFunc などから static helper method を包む struct を作る。
    private protected string DefineStruct(FuncFragment func,string methodName) {
        var structName = this.FuncStructName(methodName);
        if(!func.CanGenerateMethod) {
            return string.Empty;
        }

        return $$"""
            private struct {{structName}}{
                {{func.DefineMethod(methodName)}}
            }
            """;
    }

    // 既に struct 名が決まっているケース向けの共通版。
    private protected static string DefineStruct(string structName, FuncFragment func, string methodName) {
        if(!func.CanGenerateMethod) {
            return string.Empty;
        }

        return $$"""
            private struct {{structName}}{
                {{func.DefineMethod(methodName)}}
            }
            """;
    }

    // ParameterFunc は既存 delegate を使うので struct 生成は不要。
    private protected static string FuncDefinition(string structName,FuncFragment func,string methodName) {
        return func is ParameterFunc
            ? string.Empty
            : DefineStruct(structName,func,methodName);
    }

    // selector/predicate の呼び出しを、可能ならその場の式へ展開する。
    private protected string FuncInvoke(FuncFragment func,string methodName,params string[] arguments) {
        var argumentList = string.Join(", ",arguments);
        if(func is ParameterFunc) {
            return $"{methodName}({argumentList})";
        }
        if(func.CanInlineAssign) {
            // 非 static ラムダかつ struct closure なし: primary ctor にデリゲートとして渡すので名前で呼び出す。
            if(!func.CanGenerateMethod && ContainingTypeName is null && func.CapturedLocals.Length == 0) {
                return $"{methodName}({argumentList})";
            }
            // static ラムダ、@this struct closure、またはキャプチャローカル struct closure:
            // ラムダ本体を直接展開する（captured locals は primary ctor backing field として参照できる）。
            return func.RenderInlineExpression(arguments);
        }
        if(!func.CanGenerateMethod) {
            return $"{methodName}({argumentList})";
        }
        return $"{this.FuncStructName(methodName)}.{methodName}({argumentList})";
    }

    // 代入文が必要なときの共通ヘルパー。
    private protected string FuncAssign(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.CanInlineAssign) {
            // 非 static ラムダかつ struct closure なし: デリゲートフォールバック。
            if(!func.CanGenerateMethod && ContainingTypeName is null && func.CapturedLocals.Length == 0) {
                return $"{destination}={this.FuncInvoke(func,methodName,arguments)};";
            }
            return func.RenderInlineAssign(destination,番号,arguments);
        }

        return $"{destination}={this.FuncInvoke(func,methodName,arguments)};";
    }

    // 各断片が持つ Current/TryGetNext/Dispose を IValueEnumerator struct にまとめる。
    private protected string DefineEnumeratorStructCore(string currentType,string disposeBody) {
        return $$"""
            {{this.GetEnumeratorStructDeclaration($"global::Seamless.Linq.Carrier.IValueEnumerator<{currentType}>")}}
                {{this.GetEnumeratorFields}}
                {{this.GetEnumeratorConstructor}}
                public {{currentType}} Current => {{this.GetCurrentExpression}};
                public bool TryGetNonEnumeratedCount(out int count) {
                    count = 0;
                    return false;
                }
                public bool TryGetSpan(out global::System.ReadOnlySpan<{{currentType}}> span) {
                    span = default;
                    return false;
                }
                public bool TryCopyTo(scoped global::System.Span<{{currentType}}> destination, global::System.Index offset) => false;
                public bool TryGetNext(out {{currentType}} current) {
                    {{this.GetTryGetNextBody}}
                }
                public void Dispose() {
                    {{disposeBody}}
                }
            }
            """;
    }

    private protected string GetEnumeratorStructDeclaration(string interfaceType) {
        return string.IsNullOrWhiteSpace(this.GetEnumeratorPrimaryConstructorParameters)
            ? $"private struct {Struct} : {interfaceType} {{"
            : $"private struct {Struct}({this.GetEnumeratorPrimaryConstructorParameters}) : {interfaceType} {{";
    }

    private protected string GetGenericEnumeratorStructDeclaration(string typeParameters,string interfaceType) {
        return string.IsNullOrWhiteSpace(this.GetEnumeratorPrimaryConstructorParameters)
            ? $"private struct {Struct}{typeParameters} : {interfaceType} {{"
            : $"private struct {Struct}{typeParameters}({this.GetEnumeratorPrimaryConstructorParameters}) : {interfaceType} {{";
    }

    // struct の TryGetNext 内で使う。static ラムダまたは struct closure ならインライン展開、それ以外はデリゲートフィールド呼び出し。
    private protected string FuncInvokeField(FuncFragment func,string methodName,params string[] arguments) {
        var argumentList = string.Join(", ",arguments);
        if(func.CanGenerateMethod || NeedsStructClosure(func)) {
            // captured locals は primary ctor backing field として struct 内で直接参照できるのでインライン展開可能。
            return func.RenderInlineExpression(arguments);
        }
        // ParameterFunc または非 static ラムダ（closure なし）: フィールドとして保持しているのでフィールド名で呼び出す。
        return $"{methodName}({argumentList})";
    }

    // フラットループ内で使う。全ラムダをインライン展開（@this が静的メソッドスコープで有効）。
    // ParameterFunc かつ UnsafeAccessorMethodName が設定されている場合はデリゲート生成なしで直接呼び出す。
    // それ以外の ParameterFunc は ParameterName をキャスト式として括弧で囲んで呼び出す。
    private protected string FuncInvokeFlat(FuncFragment func,string methodName,params string[] arguments) {
        var argumentList = string.Join(", ",arguments);
        if(func is ParameterFunc parameterFunc) {
            if(parameterFunc.UnsafeAccessorMethodName is string accessorName) {
                // UnsafeAccessor 経由で直接呼び出し（デリゲート生成なし）
                return $"{accessorName}(@this, {argumentList})";
            }
            return $"({parameterFunc.ParameterName})({argumentList})";
        }
        return func.RenderInlineExpression(arguments);
    }

    // terminal チェーンでフラットループ生成をサポートするかどうか。
    public virtual bool SupportsFlatLoop => false;

    // フラットループ生成。elementVariable に各要素を束縛し bodyCode を実行するコードを返す。
    // 末尾メソッドのスコープに展開されるため @this が使用可能。
    public virtual string GetFlatLoopCode(string elementVariable,string bodyCode) =>
        throw new System.NotSupportedException($"{this.GetType().Name} does not support flat loop code generation.");

    // Carrier 側で Func delegate をそのまま渡すための式。
    private protected string FuncDelegate(FuncFragment func,string methodName) {
        return func is ParameterFunc parameterFunc
            ? parameterFunc.ParameterName
            : func.CanGenerateMethod
                ? $"{this.FuncStructName(methodName)}.{methodName}"
                : func.RenderDelegateExpression();
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

    // FuncFragment から index 番目の引数型名を取り出す。
    private protected static string FuncParameterTypeName(FuncFragment func,int index) {
        if(func.ParameterTypeNames.Length>index&&!string.IsNullOrEmpty(func.ParameterTypeNames[index])) {
            return func.ParameterTypeNames[index];
        }
        var invoke = func.GetType().GetMethod("Invoke")
            ?? throw new InvalidOperationException("FuncFragment does not expose Invoke.");
        return FuncFragment.MetadataTypeName(invoke.GetParameters()[index].ParameterType);
    }

    // FuncFragment の戻り値型名を取り出す。
    private protected static string FuncReturnTypeName(FuncFragment func) {
        return func.ReturnTypeName;
    }
}
