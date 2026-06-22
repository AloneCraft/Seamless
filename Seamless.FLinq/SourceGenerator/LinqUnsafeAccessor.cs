using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using static Seamless.SourceGenerators.GeneratorHelpers;

using Seamless.FLinq.Linq.Fragments;
using Enumerable = Seamless.FLinq.Linq.Fragments.Enumerable;

namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator {
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
        string accessorCode;
        string callExpr;
        if(field.IsStatic) {
            accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticField, Name = \"{field.Name}\")]\nprivate static extern ref {fieldTypeName} {accessorName}({targetTypeName}? _);";
            callExpr = $"{accessorName}(null)";
        } else {
            var receiverExpr = memberAccess.Expression is ThisExpressionSyntax
                ? "@this"
                : RenderExpressionForGeneratedMethod(memberAccess.Expression,semanticModel,field.ContainingType,unsafeAccessorCollector);
            accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = \"{field.Name}\")]\nprivate static extern ref {fieldTypeName} {accessorName}({targetTypeName} @this);";
            callExpr = $"{accessorName}({receiverExpr})";
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
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({string.Join(", ",paramParts)});";
            unsafeAccessorCollector?.Add(accessorCode);
            var paramNames = method.Parameters.Select(static (_, i)=>$"__a{i}").ToArray();
            var paramList = string.Join(", ",paramNames);
            var argList = string.Join(", ",new[] { receiver }.Concat(paramNames));
            return $"(({delegateTypeName})(({paramList})=>{accessorName}({argList})))";
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
            var accessorCode = $"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = \"{method.Name}\")]\nprivate static extern {returnTypeName} {accessorName}({string.Join(", ",paramParts)});";
            unsafeAccessorCollector?.Add(accessorCode);
            var paramNames = method.Parameters.Select(static (_, i)=>$"__a{i}").ToArray();
            var lambdaParams = string.Join(", ",paramNames);
            var lambdaArgs = "null" + (paramNames.Length > 0 ? ", " + string.Join(", ",paramNames) : string.Empty);
            return $"(({delegateTypeName})(({lambdaParams})=>{accessorName}({lambdaArgs})))";
        }
    }
}
