namespace Seamless.SourceGenerator.Linq.Fragments;

public class ParameterFunc : FuncFragment {
    // 既存 delegate 変数名。inline ではなくそのまま呼び出しに使う。
    public string ParameterName = string.Empty;
    // 非 public インスタンスメソッドグループの場合に生成する UnsafeAccessor 静的 extern メソッドの名前。
    // null の場合は ParameterName をデリゲートとして呼び出す。
    public string? UnsafeAccessorMethodName = null;
    // UnsafeAccessor の [UnsafeAccessor] extern static 宣言コード。
    // LinqInvocationInterceptors クラス直下に配置する。
    public string UnsafeAccessorCode = string.Empty;
}
