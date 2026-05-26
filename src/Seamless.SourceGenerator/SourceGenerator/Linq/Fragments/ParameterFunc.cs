namespace Seamless.SourceGenerator.Linq.Fragments;

public class ParameterFunc : FuncFragment {
    // 既存 delegate 変数名。inline ではなくそのまま呼び出しに使う。
    public string ParameterName = string.Empty;
    // delegate 式が変数そのものではない場合に一度受けるローカル変数名。
    public string? HoistedLocalName = null;
    public string HoistedLocalTypeName = string.Empty;
    public string InvocationName => HoistedLocalName ?? ParameterName;
    public string HoistDeclaration =>
        HoistedLocalName is null ? string.Empty : $"{HoistedLocalTypeName} {HoistedLocalName} = {ParameterName};";
    // 非 public インスタンスメソッドグループの場合に生成する UnsafeAccessor 静的 extern メソッドの名前。
    // null の場合は ParameterName をデリゲートとして呼び出す。
    public string? UnsafeAccessorMethodName = null;
    // UnsafeAccessor の [UnsafeAccessor] extern static 宣言コード。
    // LinqInvocationInterceptors クラス直下に配置する。
    public string UnsafeAccessorCode = string.Empty;
}
