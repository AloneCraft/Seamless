namespace Seamless.FLinq.Generator;

// closure の収集と、enumerator/Factory の引数・フィールド文字列への変換を ClosureRenderPlan へ集約する。
// 出力文字列の形式は既存 Fragment と同じであり、この型は互換アダプターとして残す。
internal static class ClosureRenderSupport {
    internal static ClosureRenderPlan CreatePlan(params FuncFragment[] funcs) =>
        ClosureRenderPlan.Create(funcs);

    internal static string PrimaryConstructorSuffix(params FuncFragment[] funcs) {
        var value=CreatePlan(funcs).PrimaryConstructorParameters();
        return value.Length==0 ? string.Empty : ", "+value;
    }

    internal static string MethodParameterSuffix(params FuncFragment[] funcs) =>
        CreatePlan(funcs).MethodParameterSuffix();

    internal static string ArgumentsSuffix(params FuncFragment[] funcs) =>
        CreatePlan(funcs).ArgumentsSuffix();

    internal static IEnumerable<string> ArgumentExpressions(params FuncFragment[] funcs) =>
        CreatePlan(funcs).ArgumentExpressions();

    internal static string Fields(params FuncFragment[] funcs) =>
        CreatePlan(funcs).Fields().Render();

    internal static string ArgumentsSuffixForConstructorBody(params FuncFragment[] funcs) =>
        CreatePlan(funcs).ConstructorBodyArguments();

    internal static string HoistedLocalDeclarations(params FuncFragment[] funcs) =>
        CreatePlan(funcs).HoistedLocalDeclarations().Render();

    internal static string StaticDelegateLocalDeclarations(params FuncFragment[] funcs) =>
        CreatePlan(funcs).StaticDelegateLocalDeclarations().Render();

    internal static string ArgumentsSuffixForFactory(params FuncFragment[] funcs) =>
        CreatePlan(funcs).FactoryArguments();
}
