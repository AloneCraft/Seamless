using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.Database.Generator;

/// <summary>
/// 関係代入(Document/refactoring-designs/20261117-関係代入.md)の誤りをコンパイルエラーにする。
/// <list type="bullet">
/// <item><description>SDB004: 表ではない式(Where・Union・Except・Intersect の結果、new(…)、コレクション式)への書き込み(決定7)。</description></item>
/// <item><description>SDB005: 同じメソッドの中で取引(tx)を開いている範囲から、db 側の表(コンテナ・スキーマの表のプロパティ)に触る(決定4)。</description></item>
/// </list>
/// 別のメソッドを経由するなど、ここで見えないものは実行時の例外で守る。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RelationalAssignmentAnalyzer:DiagnosticAnalyzer{
    private const string Category="Seamless.Database.Generator";
    internal static readonly DiagnosticDescriptor WriteToExpression=new(
        id:"SDB004",
        title:"A table expression cannot be written",
        messageFormat:"'{0}' writes to a table expression, not a table. Assign the expression to a table property instead",
        category:Category,defaultSeverity:DiagnosticSeverity.Error,isEnabledByDefault:true);
    internal static readonly DiagnosticDescriptor DatabaseTableInTransaction=new(
        id:"SDB005",
        title:"A database table is used while a transaction is open",
        messageFormat:"'{0}' uses a database table while transaction '{1}' is open. Use the transaction's table instead",
        category:Category,defaultSeverity:DiagnosticSeverity.Error,isEnabledByDefault:true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics=>[WriteToExpression,DatabaseTableInTransaction];

    public override void Initialize(AnalysisContext context){
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation,OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeAssignment,OperationKind.SimpleAssignment,OperationKind.CompoundAssignment);
        context.RegisterOperationAction(AnalyzePropertyReference,OperationKind.PropertyReference);
    }

    private static readonly HashSet<string> WritingMethods=new(StringComparer.Ordinal){
        "Add","Remove","Assign","AssignUnion","AssignExcept","AssignIntersect","AssignWhere","AssignRows","Update","Merge","Replace",
    };

    // 表の型(Seamless.Database.Table`1・Table`2)か、その派生。
    private static bool IsTableType(ITypeSymbol? type){
        for(var current=type as INamedTypeSymbol;current is not null;current=current.BaseType){
            if(current.OriginalDefinition.ContainingNamespace?.ToDisplayString()=="Seamless.Database"&&current.OriginalDefinition.MetadataName is "Table`2" or "Table`1"){return true;}
        }
        return false;
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context){
        var invocation=(IInvocationOperation)context.Operation;
        var method=invocation.TargetMethod;
        if(!WritingMethods.Contains(method.Name)){return;}
        IOperation? receiver=invocation.Instance;
        // 拡張メソッド Remove(行)(TableExtensions)は最初の引数が表。
        if(method.IsExtensionMethod&&method.ContainingType.Name=="TableExtensions"&&invocation.Arguments.Length>0){receiver=invocation.Arguments[0].Value;}
        if(receiver is null||!IsTableType(receiver.Type)){return;}
        if(IsExpression(receiver)){context.ReportDiagnostic(Diagnostic.Create(WriteToExpression,invocation.Syntax.GetLocation(),invocation.Syntax.ToString()));}
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context){
        var assignment=(IAssignmentOperation)context.Operation;
        // 添字の set(T.Union(rows)[key] = row)。
        if(assignment.Target is IPropertyReferenceOperation{Property.IsIndexer:true,Instance:{ } instance}&&IsTableType(instance.Type)&&IsExpression(instance)){
            context.ReportDiagnostic(Diagnostic.Create(WriteToExpression,assignment.Syntax.GetLocation(),assignment.Syntax.ToString()));
        }
    }

    // 表ではない式か。変数に入れた値は実行時に見分ける(書き込むと例外)。
    private static bool IsExpression(IOperation operation){
        while(operation is IConversionOperation{IsImplicit:true} conversion){operation=conversion.Operand;}
        while(operation is IParenthesizedOperation parenthesized){operation=parenthesized.Operand;}
        return operation switch{
            IInvocationOperation{TargetMethod:{IsStatic:false,Name:"Where" or "Union" or "Except" or "Intersect"} method}=>IsTableType(method.ContainingType),
            IBinaryOperation{OperatorMethod:{ } method}=>IsTableType(method.ContainingType)&&IsTableType(operation.Type),
            IObjectCreationOperation creation=>IsTableType(creation.Type),
            ICollectionExpressionOperation collection=>IsTableType(collection.Type),
            IConversionOperation{OperatorMethod:not null} conversion=>IsTableType(conversion.Type),
            _=>false,
        };
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context){
        var reference=(IPropertyReferenceOperation)context.Operation;
        var property=reference.Property;
        if(property.IsIndexer||!IsTableType(property.Type)||!IsDatabaseOwner(property.ContainingType)){return;}
        if(OpenTransaction(reference.Syntax,context.Operation.SemanticModel!,context.CancellationToken) is not{} transaction){return;}
        context.ReportDiagnostic(Diagnostic.Create(DatabaseTableInTransaction,reference.Syntax.GetLocation(),reference.Syntax.ToString(),transaction));
    }

    // コンテナ(DatabaseContainer の派生)かスキーマ(DatabaseSchema の派生)。取引の入口(TypedTransaction・SchemaView)は含まない。
    private static bool IsDatabaseOwner(INamedTypeSymbol? type){
        for(var current=type;current is not null;current=current.BaseType){
            if(current.ContainingNamespace?.ToDisplayString()=="Seamless.Database"&&current.Name is "DatabaseContainer" or "DatabaseSchema"){return true;}
        }
        return false;
    }

    private static readonly HashSet<string> BeginMethods=new(StringComparer.Ordinal){
        "BeginTransaction","BeginTransactionAsync","BeginReadOnlyTransactionAsync","BeginRead","BeginReadAsync",
    };

    // syntax を囲む範囲で、同じメソッドの中で開いた取引の名前。using で開いて、その範囲の中にあるときだけ。
    private static string? OpenTransaction(SyntaxNode syntax,SemanticModel model,global::System.Threading.CancellationToken cancellationToken){
        for(var node=syntax;node is not null;node=node.Parent){
            if(node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or AccessorDeclarationSyntax){return null;}
            if(node.Parent is UsingStatementSyntax usingStatement&&usingStatement.Statement==node){
                if(usingStatement.Declaration is{} declaration&&TransactionName(declaration,model,cancellationToken) is{} name){return name;}
                if(usingStatement.Expression is{} expression&&IsBegin(expression,model,cancellationToken)){return expression.ToString();}
            }
            if(node.Parent is BlockSyntax block&&node is StatementSyntax statement){
                var index=block.Statements.IndexOf(statement);
                for(var previous=index-1;previous>=0;previous--){
                    if(block.Statements[previous] is LocalDeclarationStatementSyntax{UsingKeyword.RawKind:(int)SyntaxKind.UsingKeyword} local
                        &&TransactionName(local.Declaration,model,cancellationToken) is{} name){return name;}
                }
            }
        }
        return null;
    }

    private static string? TransactionName(VariableDeclarationSyntax declaration,SemanticModel model,global::System.Threading.CancellationToken cancellationToken){
        foreach(var variable in declaration.Variables){
            if(variable.Initializer?.Value is{} value&&IsBegin(value,model,cancellationToken)){return variable.Identifier.ValueText;}
        }
        return null;
    }

    private static bool IsBegin(ExpressionSyntax expression,SemanticModel model,global::System.Threading.CancellationToken cancellationToken){
        while(expression is AwaitExpressionSyntax or ParenthesizedExpressionSyntax){
            expression=expression is AwaitExpressionSyntax awaited?awaited.Expression:((ParenthesizedExpressionSyntax)expression).Expression;
        }
        if(expression is InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax{Name.Identifier.ValueText:var name}} invocation&&BeginMethods.Contains(name)
            &&model.GetSymbolInfo(invocation,cancellationToken).Symbol is IMethodSymbol method){
            // 生成された取引の入口(TypedTransaction・Reader)だけ。従来の取引(DatabaseTransaction)の中では db 側の表を使う(今までの書き方)。
            var result=method.ReturnType as INamedTypeSymbol;
            if(result is{Name:"ValueTask" or "Task",TypeArguments.Length:1}){result=result.TypeArguments[0] as INamedTypeSymbol;}
            return IsDatabaseOwner(method.ContainingType)&&result is{Name:"TypedTransaction" or "Reader"}&&IsDatabaseOwner(result.ContainingType);
        }
        return false;
    }
}
