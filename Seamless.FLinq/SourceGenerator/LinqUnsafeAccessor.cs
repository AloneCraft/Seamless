namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {
    // UnsafeAccessor extern メソッドに必要な型パラメータ列と where 制約節を求める。
    // 受け側（含有型）/戻り値/引数の型に現れる型パラメーターを集め、
    // ジェネリック含有型（例: Base<T> の protected メンバー）でも別クラスの静的 extern アクセサとして
    // 閉じられるようにする。集めた型パラメーターは傍受メソッド（ジェネリック静的メソッド）のスコープに
    // 存在するので、宣言（<T> ... where T:notnull）と呼び出し（accessor<T>(...)）の両方でそのまま使える。
    // 型パラメーターが無ければ ("","") を返し、従来どおり非ジェネリックなアクセサになる。
    private static (string TypeParameterList, string ConstraintClause) アクセサ型パラメータ(params ITypeSymbol?[] types) {
        var collected = new List<ITypeParameterSymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Collect(ITypeSymbol? type) {
            switch(type) {
                case ITypeParameterSymbol typeParameter:
                    if(seen.Add(typeParameter.Name)) {
                        collected.Add(typeParameter);
                    }
                    break;
                case IArrayTypeSymbol arrayType:
                    Collect(arrayType.ElementType);
                    break;
                case INamedTypeSymbol namedType:
                    foreach(var typeArgument in namedType.TypeArguments) {
                        Collect(typeArgument);
                    }
                    break;
                case IPointerTypeSymbol pointerType:
                    Collect(pointerType.PointedAtType);
                    break;
            }
        }
        foreach(var type in types) {
            Collect(type);
        }
        if(collected.Count==0) {
            return (string.Empty,string.Empty);
        }
        // .NET 10 時点の UnsafeAccessor は「ジェネリック型の非公開メンバー」を実行時に解決できず
        // MissingMethodException / MissingFieldException になる（method/static/field いずれも不可。
        // 非ジェネリック型は可）。コンパイルは通るが実行時に落ちるため、生成器としてはここで例外を投げ、
        // ジェネリック含有型の傍受を諦めて元実装（素の System.Linq、遅いが正しい）にフォールバックさせる。
        // 対象の非公開メンバーを internal 以上に上げれば UnsafeAccessor を介さず直接呼べるようになり、
        // この制限を回避して傍受を有効化できる（Database.md 参照）。
        throw new global::System.NotSupportedException(
            "UnsafeAccessor cannot resolve non-public members of a generic type on this runtime; "
            +"skip interception and fall back to the original method.");
    }

    // アクセサ署名に現れる型（含有型・戻り値・各引数）をまとめて返す。型パラメータ収集の入力に使う。
    private static ITypeSymbol?[] アクセサ署名型集合(IMethodSymbol method) {
        var types = new List<ITypeSymbol?> { method.ContainingType, method.ReturnsVoid ? null : method.ReturnType };
        types.AddRange(method.Parameters.Select(static parameter=>parameter.Type));
        return types.ToArray();
    }

    /// <summary>
    /// 非 public フィールドアクセスを <c>UnsafeAccessor</c> 経由の呼び出し式に変換する。
    /// static フィールドは <c>StaticField</c> アクセサ、インスタンスフィールドは <c>Field</c> アクセサを生成し、
    /// アクセサ宣言を <paramref name="unsafeAccessorCollector"/> に追加する。
    /// アクセサは <c>ref {FieldType}</c> を返すため読み書きの両方に使える。
    /// </summary>
    private static string RenderFieldForwardExpression(IFieldSymbol field,MemberAccessExpressionSyntax memberAccess,SemanticModel semanticModel,List<string>? unsafeAccessorCollector = null) {
        var typeName = SanitizeIdentifier(field.ContainingType.Name);
        var fieldName = SanitizeIdentifier(field.Name);
        var accessorName = $"__Field_{typeName}_{fieldName}";
        var targetTypeName = field.ContainingType.ToDisplayString(完全修飾形式);
        var fieldTypeName = field.Type.ToDisplayString(完全修飾形式);
        // 含有型/フィールド型がジェネリック（例: Base<T>）ならアクセサをジェネリック extern にする。
        var (accessorTypeParameters,accessorConstraints) = アクセサ型パラメータ(field.ContainingType,field.Type);
        string accessorCode;
        string callExpr;
        if(field.IsStatic) {
            accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticField, Name = \"{field.Name}\")]\nprivate static extern ref {fieldTypeName} {accessorName}{accessorTypeParameters}({targetTypeName}? _){accessorConstraints};";
            callExpr = $"{accessorName}{accessorTypeParameters}(null)";
        } else {
            var receiverExpr = memberAccess.Expression is ThisExpressionSyntax
                ? "@this"
                : RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,field.ContainingType,unsafeAccessorCollector);
            accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = \"{field.Name}\")]\nprivate static extern ref {fieldTypeName} {accessorName}{accessorTypeParameters}({targetTypeName} @this){accessorConstraints};";
            callExpr = $"{accessorName}{accessorTypeParameters}({receiverExpr})";
        }
        unsafeAccessorCollector?.Add(accessorCode);
        return callExpr;
    }
    // メソッドグループ用の UnsafeAccessor 静的 extern メソッド名を返す。
    private static string GetDelegateAccessorMethodName(IMethodSymbol method) {
        var typeName = SanitizeIdentifier(method.ContainingType.Name);
        var methodName = SanitizeIdentifier(method.Name);
        return $"__DelegateAccess_{typeName}_{methodName}";
    }
    /// <summary>
    /// メソッドグループ参照をデリゲート生成式に変換する。アクセシビリティに応じて 4 通りに分岐する:
    /// <list type="bullet">
    ///   <item><description>public インスタンス: <c>((Delegate)@this.Method)</c>（直接バインド）。</description></item>
    ///   <item><description>非 public インスタンス: <c>UnsafeAccessor</c> ラムダ経由でデリゲート生成。</description></item>
    ///   <item><description>public static: 完全修飾名で直接参照。</description></item>
    ///   <item><description>非 public static: <c>UnsafeAccessor(StaticMethod)</c> ラムダ経由。</description></item>
    /// </list>
    /// いずれもリフレクションを使わない。
    /// </summary>
    private static string RenderDelegateCreateExpression(IMethodSymbol method,INamedTypeSymbol delegateType,string? receiver,List<string>? unsafeAccessorCollector = null) {
        var delegateTypeName = delegateType.ToDisplayString(完全修飾形式);
        var declaringType = method.ContainingType.ToDisplayString(完全修飾形式);
        // public インスタンスメソッド: @this.Method として直接バインド（リフレクション不要）
        if(!method.IsStatic && CanAccessFromGeneratedCode(method)) {
            return $"(({delegateTypeName}){receiver}.{method.Name})";
        }
        // 非 public インスタンスメソッド: UnsafeAccessor 経由のラムダでデリゲート生成（リフレクション不要）
        if(!method.IsStatic) {
            var accessorName = GetDelegateAccessorMethodName(method);
            var returnTypeName = method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(完全修飾形式);
            var paramParts = new List<string> { $"{declaringType} @this" };
            foreach(var param in method.Parameters) {
                paramParts.Add(ParameterDeclarationForAccessor(param));
            }
            var (accessorTypeParameters,accessorConstraints) = アクセサ型パラメータ(アクセサ署名型集合(method));
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}{accessorTypeParameters}({string.Join(", ",paramParts)}){accessorConstraints};";
            unsafeAccessorCollector?.Add(accessorCode);
            var paramNames = method.Parameters.Select(static (_, i)=>$"__a{i}").ToArray();
            var paramList = string.Join(", ",paramNames);
            var argList = string.Join(", ",new[] { receiver }.Concat(paramNames));
            return $"(({delegateTypeName})(({paramList})=>{accessorName}{accessorTypeParameters}({argList})))";
        }
        // public static メソッド: 完全修飾名で直接参照（リフレクション不要）
        if(CanAccessFromGeneratedCode(method)) {
            return $"(({delegateTypeName}){declaringType}.{method.Name})";
        }
        // 非 public static メソッド: UnsafeAccessor + lambda（リフレクション不要）
        {
            var typeName = SanitizeIdentifier(method.ContainingType.Name);
            var methodName = SanitizeIdentifier(method.Name);
            var accessorName = $"__StaticMethod_{typeName}_{methodName}";
            var returnTypeName = method.ReturnType.ToDisplayString(完全修飾形式);
            var paramParts = new List<string> { $"{declaringType}? _" };
            foreach(var param in method.Parameters) {
                paramParts.Add($"{param.Type.ToDisplayString(完全修飾形式)} {param.Name}");
            }
            var (accessorTypeParameters,accessorConstraints) = アクセサ型パラメータ(アクセサ署名型集合(method));
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}{accessorTypeParameters}({string.Join(", ",paramParts)}){accessorConstraints};";
            unsafeAccessorCollector?.Add(accessorCode);
            var paramNames = method.Parameters.Select(static (_, i)=>$"__a{i}").ToArray();
            var lambdaParams = string.Join(", ",paramNames);
            var lambdaArgs = "null" + (paramNames.Length > 0 ? ", " + string.Join(", ",paramNames) : string.Empty);
            return $"(({delegateTypeName})(({lambdaParams})=>{accessorName}{accessorTypeParameters}({lambdaArgs})))";
        }
    }
}
