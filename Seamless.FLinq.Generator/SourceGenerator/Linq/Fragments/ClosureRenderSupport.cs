namespace Seamless.FLinq;

// closure の収集と、enumerator/Factory の引数・フィールド文字列への変換を Fragment 本体から分離する。
// 出力文字列の形式は既存 Fragment と同じであり、この型は第2段階の中間表現化に向けた境界だけを提供する。
internal static class ClosureRenderSupport {
    internal static string PrimaryConstructorSuffix(params FuncFragment[] funcs) =>
        ParameterSuffix(funcs);

    internal static string MethodParameterSuffix(params FuncFragment[] funcs) =>
        ParameterSuffix(funcs);

    internal static string ArgumentsSuffix(params FuncFragment[] funcs) {
        var captures=DistinctCapturedLocals(funcs).ToArray();
        var hoistedLocals=DistinctHoistedLocals(funcs).ToArray();
        var delegates=DistinctDelegateFuncs(funcs).ToArray();
        var parts=captures.Select(static capture => capture.Name)
            .Concat(hoistedLocals.Select(static hoistedLocal => hoistedLocal.ExpressionText))
            .Concat(delegates.Select(static func => func.DelegateConstructorArg!));
        var joined=string.Join(", ",parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : ", "+joined;
    }

    internal static string Fields(params FuncFragment[] funcs) {
        var delegates=DistinctDelegateFuncs(funcs).ToArray();
        if(delegates.Length==0) {
            return string.Empty;
        }
        return string.Join("\n    ",delegates.Select(
            static func => $"private {func.DelegateTypeName} {func.DelegateFieldName}={func.DelegateFieldName};"));
    }

    internal static string ArgumentsSuffixForConstructorBody(params FuncFragment[] funcs) {
        var captures=DistinctCapturedLocals(funcs).ToArray();
        var hoistedLocals=DistinctHoistedLocals(funcs).ToArray();
        var delegates=DistinctDelegateFuncs(funcs).ToArray();
        var parts=captures.Select(static capture => $"this.{capture.Name}")
            .Concat(hoistedLocals.Select(static hoistedLocal => $"this.{hoistedLocal.Name}"))
            .Concat(delegates.Select(static func => $"this.{func.DelegateFieldName}"));
        var joined=string.Join(", ",parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : ", "+joined;
    }

    internal static string HoistedLocalDeclarations(params FuncFragment[] funcs) {
        var hoistedLocals=DistinctHoistedLocals(funcs).ToArray();
        return hoistedLocals.Length==0
            ? string.Empty
            : string.Join("\n",hoistedLocals.Select(static hoistedLocal => $"{hoistedLocal.TypeName} {hoistedLocal.Name}={hoistedLocal.ExpressionText};"));
    }

    internal static string StaticDelegateLocalDeclarations(params FuncFragment[] funcs) {
        var delegates=DistinctDelegateFuncs(funcs).ToArray();
        return delegates.Length==0
            ? string.Empty
            : string.Join("\n",delegates.Select(static func => $"{func.DelegateTypeName} {func.DelegateFieldName}={func.DelegateConstructorArg};"));
    }

    internal static string ArgumentsSuffixForFactory(params FuncFragment[] funcs) {
        var captures=DistinctCapturedLocals(funcs).ToArray();
        var hoistedLocals=DistinctHoistedLocals(funcs).ToArray();
        var delegates=DistinctDelegateFuncs(funcs).ToArray();
        var parts=captures.Select(static capture => capture.Name)
            .Concat(hoistedLocals.Select(static hoistedLocal => hoistedLocal.Name))
            .Concat(delegates.Select(static func => func.DelegateFieldName!));
        var joined=string.Join(", ",parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : ", "+joined;
    }

    private static string ParameterSuffix(params FuncFragment[] funcs) {
        var captures=DistinctCapturedLocals(funcs).ToArray();
        var hoistedLocals=DistinctHoistedLocals(funcs).ToArray();
        var delegates=DistinctDelegateFuncs(funcs).ToArray();
        var parts=captures.Select(static capture => $"{capture.TypeName} {capture.Name}")
            .Concat(hoistedLocals.Select(static hoistedLocal => $"{hoistedLocal.TypeName} {hoistedLocal.Name}"))
            .Concat(delegates.Select(static func => $"{func.DelegateTypeName} {func.DelegateFieldName}"));
        var joined=string.Join(", ",parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : ", "+joined;
    }

    private static IEnumerable<(string Name,string TypeName)> DistinctCapturedLocals(params FuncFragment[] funcs) {
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in funcs) {
            foreach(var capture in func.CapturedLocals) {
                if(names.Add(capture.Name)) {
                    yield return capture;
                }
            }
        }
    }

    private static IEnumerable<HoistedLocal> DistinctHoistedLocals(params FuncFragment[] funcs) {
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in funcs) {
            foreach(var hoistedLocal in func.HoistedLocals) {
                if(names.Add(hoistedLocal.Name)) {
                    yield return hoistedLocal;
                }
            }
        }
    }

    private static IEnumerable<FuncFragment> DistinctDelegateFuncs(params FuncFragment[] funcs) {
        var fieldNames=new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in funcs) {
            if(func.CanInvokeAsDelegate&&func.DelegateFieldName is { } name&&fieldNames.Add(name)) {
                yield return func;
            }
        }
    }
}