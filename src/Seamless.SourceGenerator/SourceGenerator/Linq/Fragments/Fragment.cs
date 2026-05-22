using System;

namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class Fragment(
    int 番号
) {
    private protected string e => $"e{番号}";
    private protected string Head=> $"Head{番号}";
    private protected string Start=> $"Start{番号}";
    private protected string Skip=> $"Skip{番号}";
    private protected string End=> $"End{番号}";
    protected string result=> $"result{番号}";
    private protected string Struct=> $"Struct{番号}";
    private protected string lookup=> $"lookup{番号}";
    private protected string item=> $"ite{番号}";
    private protected string temp=> $"temp{番号}";
    private protected string index=> $"index{番号}";
    private protected string key=> $"key{番号}";
    private protected string group=> $"group{番号}";
    //private protected string seed=> $"seed{番号}";
    private protected string hasValue => $"hasValue{番号}";
    private protected string ordered => $"ordered{番号}";
    private protected string currentValue => $"currentValue{番号}";
    private protected string state => $"state{番号}";
    private protected string yielded => $"yielded{番号}";
    private protected string groupIndex => $"groupIndex{番号}";
    private protected string outerValue => $"outerValue{番号}";
    // public readonly string StructDefinition = StructDefinition;
    // public readonly string BeginGlobal = BeginGlobal;
    // public readonly string BeginLoopOutside = BeginLoopOutside;
    // public readonly string BeginLoop = BeginLoop;
    // public readonly string BeginLoopInside = BeginLoopInside;
    // public readonly string EndLoopInside = EndLoopInside;
    // public readonly string EndLoop = EndLoop;
    // public readonly string EndLoopOutside = EndLoopOutside;
    // public readonly string EndGlobal = EndGlobal;
    public abstract string StructMethod { get; }
    public abstract string GetEnumeratorStruct { get; }
    public abstract string GetTryGetNextBody { get; }
    public abstract string GetCurrentExpression { get; }
    public string EnumeratorTypeName => Struct;
    public virtual string GetEnumeratorExpression => throw new NotSupportedException($"{this.GetType().Name} does not support Enumerator mode expression generation.");
    public virtual string GetEnumeratorFields => string.Empty;
    public virtual string GetEnumeratorConstructor => string.Empty;
    public virtual bool CanExpandInLocalMethodBody => false;
    public abstract string GetBeginGlobal{ get; }
    public abstract string GetBeginLoopOutside{ get; }
    public abstract string GetBeginLoop{ get; }
    public abstract string GetBeginLoopInside{ get; }
    public abstract string GetEndLoopInside{ get; }
    public abstract string GetEndLoop{ get; }
    public abstract string GetEndLoopOutside{ get; }
    public abstract string GetEndGlobal { get; }
    //public abstract string Result{ get; }
    public virtual string Result => result;
    public virtual string BeginGlobal => GetBeginGlobal;
    public virtual string BeginLoopOutside => GetBeginLoopOutside;
    public virtual string BeginLoop => GetBeginLoop;
    public virtual string BeginLoopInside => GetBeginLoopInside;
    public virtual string EndLoopInside => GetEndLoopInside;
    public virtual string EndLoop => GetEndLoop;
    public virtual string EndLoopOutside => GetEndLoopOutside;
    public virtual string EndGlobal => GetEndGlobal;
    private protected static string var_List(string name,string ElementType) =>
        $"var {name}=new global::System.Collections.Generic.List<{ElementType}>();";
    private protected string FuncStructName(string methodName) => $"{Struct}_{methodName}";
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

    private protected static string FuncDefinition(string structName,FuncFragment func,string methodName) {
        return func is ParameterFunc
            ? string.Empty
            : DefineStruct(structName,func,methodName);
    }

    private protected string FuncInvoke(FuncFragment func,string methodName,params string[] arguments) {
        var argumentList = string.Join(", ",arguments);
        if(func is ParameterFunc parameterFunc) {
            return $"({parameterFunc.ParameterName})({argumentList})";
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

    private protected string FuncAssign(FuncFragment func,string methodName,string destination,params string[] arguments) {
        if(func.CanInlineAssign) {
            // 代入文を作る側も同じ理由で helper メソッド化しない。
            // `destination = selector(item);` をそのまま関数呼び出しへせず、
            // ラムダ本体を埋め込んだ代入文へ変換する。
            return func.RenderInlineAssign(destination,番号,arguments);
        }

        return $"{destination}={this.FuncInvoke(func,methodName,arguments)};";
    }

    private protected string DefineEnumeratorStructCore(string currentType,string disposeBody) {
        return $$"""
            private struct {{Struct}} : global::Seamless.Linq.Carrier.IValueEnumerator<{{currentType}}> {
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

    private protected string FuncDelegate(FuncFragment func,string methodName) {
        return func is ParameterFunc parameterFunc
            ? parameterFunc.ParameterName
            : $"{this.FuncStructName(methodName)}.{methodName}";
    }

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

    private protected static string FuncParameterTypeName(FuncFragment func,int index) {
        if(func.ParameterTypeNames.Length>index&&!string.IsNullOrEmpty(func.ParameterTypeNames[index])) {
            return func.ParameterTypeNames[index];
        }
        var invoke = func.GetType().GetMethod("Invoke")
            ?? throw new InvalidOperationException("FuncFragment does not expose Invoke.");
        return FuncFragment.MetadataTypeName(invoke.GetParameters()[index].ParameterType);
    }

    private protected static string FuncReturnTypeName(FuncFragment func) {
        return func.ReturnTypeName;
    }
}
