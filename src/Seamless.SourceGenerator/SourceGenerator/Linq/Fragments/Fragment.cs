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
            // static ラムダはローカルメソッド本文の展開では helper 呼び出しにしない。
            // 以前は `Struct1_selector.selector(ite2)` のような形を生成していたが、
            // それだと「最終的に何を計算しているか」が生成コードから読み取りづらい。
            // ここでラムダ本体の式へ直接落とすことで、
            // `result0.Add(((ite2) * 10));` のような形にできる。
            //
            // なお ParameterFunc は実行時 delegate なのでインライン化できず、
            // その場合だけ従来どおり delegate 呼び出しを残す。
            return func.RenderInlineExpression(arguments);
        }
        return $"{this.FuncStructName(methodName)}.{methodName}({argumentList})";
    }

    // 代入文が必要なときの共通ヘルパー。
    private protected string FuncAssign(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.CanInlineAssign) {
            // 代入文を作る側も同じ理由で helper メソッド化しない。
            // `destination = selector(item);` をそのまま関数呼び出しへせず、
            // ラムダ本体を埋め込んだ代入文へ変換する。
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
