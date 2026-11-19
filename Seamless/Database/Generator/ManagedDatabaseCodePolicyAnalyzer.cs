using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Seamless.Database.Generator;

/// <summary>
/// Manager が管理する DB(プロジェクトの設定に SeamlessManagedDatabase=true の印がある)の利用者コードの使用制限。
/// SDK のビルドでも Manager の中のコンパイルでも、同じ規則でエラーにする(規則の置き場はここだけ)。
/// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階7(1)。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManagedDatabaseCodePolicyAnalyzer:DiagnosticAnalyzer{
    /// <summary>プロジェクトの設定の印。解析器へ渡すには CompilerVisibleProperty にも同じ名前を書く。</summary>
    public const string ManagedDatabaseProperty="SeamlessManagedDatabase";
    public const string DiagnosticId="SEAMLESSMANAGED001";
    private static readonly DiagnosticDescriptor Rule=new(DiagnosticId,"Manager が管理する DB で使えないコード","{0}","Seamless.Management",DiagnosticSeverity.Error,isEnabledByDefault:true);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics=>[Rule];

    public override void Initialize(AnalysisContext context){
        // 生成器の出力(DB 生成器・FLinq の傍受コード)は検査しない。利用者が書いたコードだけを見る。
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start=>{
            if(!start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property."+ManagedDatabaseProperty,out var value)||!string.Equals(value?.Trim(),"true",StringComparison.OrdinalIgnoreCase)){return;}
            start.RegisterSemanticModelAction(analysis=>{
                foreach(var (node,reason) in Check(analysis.SemanticModel,analysis.CancellationToken)){
                    analysis.ReportDiagnostic(Diagnostic.Create(Rule,node.GetLocation(),reason));
                }
            });
        });
    }

    /// <summary>1つの構文木を検査し、禁止の理由ごとに節を返す。</summary>
    public static IEnumerable<(SyntaxNode Node,string Reason)> Check(SemanticModel model,global::System.Threading.CancellationToken cancellation){
        foreach(var node in model.SyntaxTree.GetRoot(cancellation).DescendantNodes()){
            cancellation.ThrowIfCancellationRequested();
            if(Reason(node,model) is{} reason){yield return (node,reason);}
        }
    }

    private static string? Reason(SyntaxNode node,SemanticModel model){
        string? reason=null;
        if(node is TypeSyntax syntax&&model.GetTypeInfo(syntax).Type is INamedTypeSymbol referenced&&!AllowedSystemType(referenced)){reason="DB処理・計算の対応範囲外の型です: "+referenced.ToDisplayString();}
        if(node is WithExpressionSyntax withExpression&&model.GetTypeInfo(withExpression).Type?.Name=="DatabaseContainerOptions"){reason="DBの実行設定はサーバーから渡された値をそのまま使用してください。";}
        if(node is PrimaryConstructorBaseTypeSyntax primaryBase&&model.GetTypeInfo(primaryBase.Type).Type is INamedTypeSymbol primaryType&&IsDatabaseType(primaryType)&&!PassesOptions(primaryBase.ArgumentList.Arguments,model)){reason="DBの基底コンストラクターにはサーバーから渡されたoptionsだけを渡してください。";}
        if(node is ConstructorDeclarationSyntax constructor&&model.GetDeclaredSymbol(constructor)?.ContainingType is{} constructorType&&IsDatabaseType(constructorType)&&(constructor.Initializer is null||!PassesOptions(constructor.Initializer.ArgumentList.Arguments,model))){reason="DBコンストラクターはoptionsを基底へ直接引き渡してください。";}
        if(node is ClassDeclarationSyntax declaration&&model.GetDeclaredSymbol(declaration) is{} declaredType&&IsDatabaseType(declaredType)){
            if(declaration.ParameterList is not null&&declaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().Any()!=true||declaration.ParameterList is null&&declaredType.InstanceConstructors.All(item=>item.IsImplicitlyDeclared)){reason="DBにはoptionsを直接引き渡す明示コンストラクターが必要です。";}
        }
        if(node is UnsafeStatementSyntax or PointerTypeSyntax or FunctionPointerTypeSyntax or StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax or TypeOfExpressionSyntax){reason="unsafe/ポインター/stackalloc/typeofは対応範囲外です。";}
        if(node is MethodDeclarationSyntax method&&(method.Modifiers.Any(SyntaxKind.ExternKeyword)||method.Modifiers.Any(SyntaxKind.AsyncKeyword)||method.Modifiers.Any(SyntaxKind.UnsafeKeyword))){reason="extern・async・unsafeは対応範囲外です。";}
        if(node is IdentifierNameSyntax identifier&&identifier.Identifier.ValueText=="dynamic"){reason="dynamicは対応範囲外です。";}
        if(node is InvocationExpressionSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax or MemberAccessExpressionSyntax or AttributeSyntax){
            var symbol=model.GetSymbolInfo(node).Symbol;var type=symbol?.ContainingType;var ns=type?.ContainingNamespace.ToDisplayString()??"";
            if(type is not null&&!AllowedSystemType(type)){reason="DB処理・計算の対応範囲外のAPIです: "+symbol?.ToDisplayString();}
            if(ns.StartsWith("Seamless.Management",StringComparison.Ordinal)&&type?.ToDisplayString()!="Seamless.Management.Server.ManagementExecutionContext"){reason="管理用APIは問い合わせコードから利用できません。";}
            if(ns.StartsWith("Seamless.Execution",StringComparison.Ordinal)||type?.ToDisplayString()=="Seamless.Database.DatabaseContainerOptions"&&node is not IdentifierNameSyntax){reason="保存先・実行環境は管理サーバーが設定します。";}
            if(ns.StartsWith("Seamless.Transactions",StringComparison.Ordinal)||ns.StartsWith("Seamless.Collections.Tsavorite",StringComparison.Ordinal)||ControlsDatabaseTransaction(symbol)||symbol?.Name.Contains("Gateway")==true&&ns.StartsWith("Seamless.Database",StringComparison.Ordinal)){reason="取引・永続化・DBの寿命は管理サーバーが制御します。";}
            if(node is BaseObjectCreationExpressionSyntax&&type is not null&&(IsDatabaseType(type)||type.ToDisplayString()=="Seamless.Management.Server.ManagementExecutionContext")){
                reason="DBと取引の実行の文脈は管理サーバーから受け取ってください。";
            }
            if(ns.StartsWith("System.IO",StringComparison.Ordinal)||ns.StartsWith("System.Net",StringComparison.Ordinal)||ns.StartsWith("System.Reflection",StringComparison.Ordinal)||ns.StartsWith("System.Runtime.InteropServices",StringComparison.Ordinal)||ns.StartsWith("System.Runtime.Loader",StringComparison.Ordinal)||ns.StartsWith("System.Threading",StringComparison.Ordinal)&&type?.ToDisplayString()!="System.Threading.CancellationToken"||ns.StartsWith("Microsoft",StringComparison.Ordinal)
                ||ns.StartsWith("System.Linq.Expressions",StringComparison.Ordinal)||type?.ToDisplayString() is "System.AppDomain" or "System.Environment" or "System.GC" or "System.Console" or "System.Diagnostics.Process" or "System.Type" or "System.Activator" or "System.Delegate"||symbol?.Name=="GetType"){reason="DB処理・計算から利用できないAPIです: "+symbol?.ToDisplayString();}
        }
        return reason;
    }
    private static bool ControlsDatabaseTransaction(ISymbol? symbol){
        if(symbol?.ContainingType is not{} type){return false;}
        if(type.ToDisplayString()=="Seamless.Database.DatabaseQuery"&&symbol.Name=="Execute"){return true;}
        if(type.ContainingType is{} owner&&IsDatabaseType(owner)&&type.IsImplicitlyDeclared==false&&
            type.Name is "TypedTransaction" or "Reader"&&symbol.Name is "BeginTransaction" or "BeginTransactionAsync" or "Commit" or "CommitAsync" or "Rollback" or "RollbackAsync" or "Dispose" or "DisposeAsync" or "ImportTablesAsync"){
            return true;
        }
        return IsDatabaseType(type)&&symbol.Name is "Dispose" or "Options" or "Queries" or
            "BeginTransaction" or "BeginTransactionAsync" or "BeginReadOnlyTransactionAsync" or "BeginRead" or "BeginReadAsync" or "BeginReadSession" or
            "BeginTransactionScopeAsync" or "BeginReadOnlyTransactionScopeAsync" or "BeginRelationImport" or "BeginUnpublishedSnapshot" or "ImportTablesAsync";
    }
    private static bool PassesOptions(SeparatedSyntaxList<ArgumentSyntax> arguments,SemanticModel model)=>arguments.Count==1&&arguments[0].Expression is IdentifierNameSyntax&&model.GetSymbolInfo(arguments[0].Expression).Symbol is IParameterSymbol parameter&&parameter.Type.Name=="DatabaseContainerOptions"&&parameter.Type.ContainingNamespace.ToDisplayString()=="Seamless.Database";
    private static bool IsDatabaseType(INamedTypeSymbol type){for(var parent=type.BaseType;parent is not null;parent=parent.BaseType){if(parent.ToDisplayString()=="Seamless.Database.DatabaseContainer"){return true;}}return type.ToDisplayString()=="Seamless.Database.DatabaseContainer";}
    private static bool AllowedSystemType(INamedTypeSymbol type){
        var ns=type.ContainingNamespace.ToDisplayString();if(ns=="System"){return type.Name is not "AppDomain" and not "AppContext" and not "Environment" and not "GC" and not "Console" and not "Type" and not "Activator" and not "Delegate" and not "RuntimeMethodHandle" and not "RuntimeTypeHandle";}
        if(ns.StartsWith("Tsavorite",StringComparison.Ordinal)||ns.StartsWith("Seamless.Database.Client",StringComparison.Ordinal)||ns.StartsWith("Seamless.Transport",StringComparison.Ordinal)){return false;}if(!ns.StartsWith("System.",StringComparison.Ordinal)){return true;}
        if(ns=="System.Collections.Generic"&&type.Name is "IAsyncEnumerable" or "IAsyncEnumerator"){return false;}
        if(ns=="System.Linq"||ns.StartsWith("System.Collections",StringComparison.Ordinal)||ns.StartsWith("System.Numerics",StringComparison.Ordinal)||ns.StartsWith("System.Globalization",StringComparison.Ordinal)||ns.StartsWith("System.Text",StringComparison.Ordinal)||ns.StartsWith("System.Buffers",StringComparison.Ordinal)){return true;}
        if(ns=="System.Threading"&&type.Name=="CancellationToken"){return true;}
        if(ns=="System.Diagnostics"&&type.Name=="Stopwatch"){return true;}
        if(ns=="System.Runtime.CompilerServices"&&type.Name is "MethodImplAttribute" or "MethodImplOptions" or "CallerMemberNameAttribute" or "CallerArgumentExpressionAttribute"){return true;}
        return false;
    }
}
