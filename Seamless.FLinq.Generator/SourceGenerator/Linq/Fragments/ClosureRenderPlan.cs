namespace Seamless.FLinq;

/// <summary>
/// FuncFragment群から抽出したclosureの順序付き描画情報です。
/// 重複除去は最初に現れた要素を保持し、従来のClosureRenderSupportと同じ順序を保証します。
/// </summary>
internal sealed class ClosureRenderPlan {
    private ClosureRenderPlan(
        ImmutableArray<(string Name,string TypeName)> captures,
        ImmutableArray<HoistedLocal> hoistedLocals,
        ImmutableArray<FuncFragment> delegates) {
        this.Captures=captures;
        this.HoistedLocals=hoistedLocals;
        this.Delegates=delegates;
    }

    internal ImmutableArray<(string Name,string TypeName)> Captures { get; }
    internal ImmutableArray<HoistedLocal> HoistedLocals { get; }
    internal ImmutableArray<FuncFragment> Delegates { get; }

    internal static ClosureRenderPlan Create(params FuncFragment[] funcs) {
        var captures=new List<(string Name,string TypeName)>();
        var captureNames=new HashSet<string>(StringComparer.Ordinal);
        var hoistedLocals=new List<HoistedLocal>();
        var hoistedNames=new HashSet<string>(StringComparer.Ordinal);
        var delegates=new List<FuncFragment>();
        var delegateNames=new HashSet<string>(StringComparer.Ordinal);

        foreach(var func in funcs) {
            foreach(var capture in func.CapturedLocals) {
                if(captureNames.Add(capture.Name)) {
                    captures.Add(capture);
                }
            }
            foreach(var hoistedLocal in func.HoistedLocals) {
                if(hoistedNames.Add(hoistedLocal.Name)) {
                    hoistedLocals.Add(hoistedLocal);
                }
            }
            if(func.CanInvokeAsDelegate
                &&func.DelegateFieldName is { Length:>0 } delegateName
                &&delegateNames.Add(delegateName)) {
                delegates.Add(func);
            }
        }

        return new ClosureRenderPlan(
            captures.ToImmutableArray(),
            hoistedLocals.ToImmutableArray(),
            delegates.ToImmutableArray());
    }

    internal IEnumerable<string> ArgumentExpressions() {
        foreach(var capture in this.Captures) {
            yield return capture.Name;
        }
        foreach(var hoistedLocal in this.HoistedLocals) {
            yield return hoistedLocal.ExpressionText;
        }
        foreach(var func in this.Delegates) {
            yield return func.DelegateConstructorArg!;
        }
    }

    internal string PrimaryConstructorParameters() => string.Join(", ",this.ParameterDeclarations());

    internal string MethodParameterSuffix() {
        var value=this.PrimaryConstructorParameters();
        return value.Length==0 ? string.Empty : ", "+value;
    }

    internal string ArgumentsSuffix() {
        var value=string.Join(", ",this.ArgumentExpressions());
        return value.Length==0 ? string.Empty : ", "+value;
    }

    internal string ConstructorBodyArguments() {
        var values=this.Captures.Select(static capture => $"this.{capture.Name}")
            .Concat(this.HoistedLocals.Select(static local => $"this.{local.Name}"))
            .Concat(this.Delegates.Select(static func => $"this.{func.DelegateFieldName}"));
        var value=string.Join(", ",values);
        return value.Length==0 ? string.Empty : ", "+value;
    }

    internal string FactoryArguments() {
        var values=this.Captures.Select(static capture => capture.Name)
            .Concat(this.HoistedLocals.Select(static local => local.Name))
            .Concat(this.Delegates.Select(static func => func.DelegateFieldName!));
        var value=string.Join(", ",values);
        return value.Length==0 ? string.Empty : ", "+value;
    }

    internal GeneratedCodeBlock Fields() {
        return GeneratedCodeBlock.Leaf(string.Join("\n",this.Delegates.Select(static func =>
            $"private {func.DelegateTypeName} {func.DelegateFieldName}={func.DelegateFieldName};")));
    }

    internal GeneratedCodeBlock HoistedLocalDeclarations() {
        return GeneratedCodeBlock.Leaf(string.Join("\n",this.HoistedLocals.Select(static local =>
            $"{local.TypeName} {local.Name}={local.ExpressionText};")));
    }

    internal GeneratedCodeBlock StaticDelegateLocalDeclarations() {
        return GeneratedCodeBlock.Leaf(string.Join("\n",this.Delegates.Select(static func =>
            $"{func.DelegateTypeName} {func.DelegateFieldName}={func.DelegateConstructorArg};")));
    }

    private IEnumerable<string> ParameterDeclarations() {
        foreach(var capture in this.Captures) {
            yield return $"{capture.TypeName} {capture.Name}";
        }
        foreach(var hoistedLocal in this.HoistedLocals) {
            yield return $"{hoistedLocal.TypeName} {hoistedLocal.Name}";
        }
        foreach(var func in this.Delegates) {
            yield return $"{func.DelegateTypeName} {func.DelegateFieldName}";
        }
    }
}
