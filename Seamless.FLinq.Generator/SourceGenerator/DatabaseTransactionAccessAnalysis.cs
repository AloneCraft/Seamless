using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Security.Cryptography;
namespace Seamless.FLinq.Generator;

/// <summary>取引境界を所有するソース全体を保守的に解析する。不明な経路は通常の更新可能取引を保つ。</summary>
internal sealed class DatabaseTransactionAccessAnalysis{
    private readonly Compilation compilation;
    private readonly CancellationToken cancellationToken;
    private readonly HashSet<ISymbol> visiting=new(SymbolEqualityComparer.Default);
    private readonly HashSet<ISymbol> initializing=new(SymbolEqualityComparer.Default);
    private readonly List<string> steps=[];
    private readonly HashSet<string> recorded=new(StringComparer.Ordinal);
    private int remaining=10000;
    private bool unsafeAccess;
    private DatabaseTransactionAccessAnalysis(Compilation compilation,CancellationToken cancellationToken){this.compilation=compilation;this.cancellationToken=cancellationToken;}

    private static (IMethodSymbol Begin,DatabaseTransactionAccessAnalysis Analysis)? AnalyzeBegin(SemanticModel model,InvocationExpressionSyntax invocation,CancellationToken cancellationToken){
        if(model.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol{ Name:"BeginTransactionAsync",IsStatic:false,IsVirtual:false } begin
            ||begin.Parameters.Length!=1||begin.Parameters[0].Type.ToDisplayString()!="System.Threading.CancellationToken"
            ||!IsDatabase(begin.ContainingType)||HasTypeParameter(begin.ContainingType)
            ||FLinqIncrementalGenerator.GenerationPipeline.ContainsGeneratedTypeAccessibilityRisk(begin.ContainingType)
            ||!begin.DeclaringSyntaxReferences.Any(static reference=>reference.SyntaxTree.FilePath.EndsWith(".Container.g.cs",StringComparison.Ordinal))
            ||!begin.ContainingType.GetMembers("BeginReadOnlyTransactionAsync").OfType<IMethodSymbol>().Any()){return null;}
        var ownerSyntax=invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if(ownerSyntax is null||model.GetDeclaredSymbol(ownerSyntax,cancellationToken) is not IMethodSymbol owner
            ||!FLinqIncrementalGenerator.LinqRewrite判定(owner,model.Compilation)){return null;}
        var variable=invocation.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        IEnumerable<StatementSyntax>? statements=null;
        if(variable?.Initializer?.Value is AwaitExpressionSyntax{Expression:InvocationExpressionSyntax awaited}&&ReferenceEquals(awaited,invocation)
            &&variable.Parent?.Parent is LocalDeclarationStatementSyntax declaration&&declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)&&declaration.Parent is BlockSyntax block){
            statements=block.Statements.Skip(block.Statements.IndexOf(declaration)+1);
        }else if(variable?.Parent?.Parent is UsingStatementSyntax statement){statements=[statement.Statement];}
        var analysis=new DatabaseTransactionAccessAnalysis(model.Compilation,cancellationToken);
        if(statements is null){analysis.Unsafe(invocation,"不明: await usingで所有する取引範囲を確定できないため、更新可能な開始を保持");}
        else{foreach(var statement in statements){analysis.Visit(model.GetOperation(statement,cancellationToken));}}
        return(begin,analysis);
    }
    internal static bool IsReadOnlyBegin(SemanticModel model,InvocationExpressionSyntax invocation){
        var decision=AnalyzeBegin(model,invocation,default);return decision.HasValue&&!decision.Value.Analysis.unsafeAccess;
    }
    internal static string? ReadOnlyInterceptor(SemanticModel model,InvocationExpressionSyntax invocation,CancellationToken cancellationToken){
        if(AnalyzeBegin(model,invocation,cancellationToken) is not { } decision){return null;}
        var(begin,analysis)=decision;
        var location=Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(model,invocation,cancellationToken);if(location is null){return null;}
        var attribute=Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(location);
        using var hash=SHA256.Create();var suffix=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(invocation.SyntaxTree.FilePath+":"+invocation.SpanStart))).Replace("-","");
        var type=begin.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);var result=begin.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var target="BeginReadOnlyTransactionAsync";var kind="ReadOnly";var prefix="__DatabaseReadOnly_";
        if(analysis.unsafeAccess){target="BeginTransactionAsync";kind="WritableOrUnknown";prefix="__DatabaseTransaction_";}
        var plan=new StringBuilder();
        plan.AppendLine("// [アクセス判定] "+kind+": "+Clean(invocation.SyntaxTree.FilePath)+":"+(invocation.GetLocation().GetLineSpan().StartLinePosition.Line+1));
        foreach(var step in analysis.steps){plan.AppendLine("// "+step);}
        plan.AppendLine("// [変形] BeginTransactionAsync -> "+target+"。既存取引を受け取るだけの呼出しでは取引全体を変更しない。");
        plan.AppendLine("// [並列] 取引の同時実行はOptions.TransactionConcurrencyで選択。同一取引の公開呼出しは一つずつ。読取判定だけでworkerを追加しない。");
        plan.AppendLine("// [キュー・同期] 開始は回収中のdomain入場待ちを経る。確定キュー/WALへの投入はCommitAsyncで行い、この開始行では行わない。");
        plan.AppendLine("// [資源・不変条件] 同じsnapshotを全表へ共有。終了時にscopeが参照を解放し、競合時に業務処理を自動再実行しない。");
        var debug=Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(plan.ToString(),true);
        return "// <auto-generated/>\n#nullable enable\nnamespace Intercept{\n"+plan+"internal static class "+prefix+suffix+"{\n#if DEBUG\n// [Debug] 静的判断をメタデータ定数へ保存。実行時の型や取引を保持せず、ログ出力もしない。\ninternal const string DebugPlan="+debug+";\n#endif\n"+attribute+"\npublic static "+result+" Begin(this "+type+" receiver,global::System.Threading.CancellationToken cancellationToken){\n// [委譲] 非asyncの入口を維持し、AsyncLocal holderの設定を呼出し側の実行コンテキストに残す。取消tokenは同じ値を転送。\nreturn receiver."+target+"(cancellationToken);\n}\n}}\n";
    }
    private static string Clean(string text)=>GeneratedExecutionAnnotations.Clean(text);
    private void Note(SyntaxNode syntax,string message){
        var span=syntax.GetLocation().GetLineSpan();var key=syntax.SyntaxTree.FilePath+":"+syntax.SpanStart+":"+message;
        if(this.recorded.Add(key)){this.steps.Add("[アクセス] "+Clean(System.IO.Path.GetFileName(span.Path))+":"+(span.StartLinePosition.Line+1)+" "+Clean(message));}
    }
    private void Unsafe(SyntaxNode syntax,string reason){this.unsafeAccess=true;this.Note(syntax,reason);}
    private static bool HasTypeParameter(ITypeSymbol type)=>type.TypeKind==TypeKind.TypeParameter||type is INamedTypeSymbol named&&named.TypeArguments.Any(HasTypeParameter);
    private static bool IsDatabase(INamedTypeSymbol? type){
        for(;type is not null;type=type.BaseType){if(type.Name=="DatabaseContainer"&&type.ContainingNamespace.ToDisplayString()=="Seamless.Database"){return true;}}return false;
    }
    private static bool IsView(ITypeSymbol? type)=>type is INamedTypeSymbol named&&named.ContainingNamespace.ToDisplayString()=="Seamless.Transactions.Collections"
        &&named.Name is "TransactionCollection" or "ReadOnlyTransactionCollection" or "TransactionRows"
        ||type is INamedTypeSymbol table&&table.ContainingNamespace.ToDisplayString()=="Seamless.Database"&&table.Name=="Table";
    private static bool IsScope(ITypeSymbol? type)=>type is INamedTypeSymbol named&&(named.Name=="TransactionScope"&&named.ContainingNamespace.ToDisplayString()=="Seamless.Transactions"
        ||named.Name=="TypedTransaction"&&IsDatabase(named.ContainingType)||named.Name.StartsWith("SchemaView",StringComparison.Ordinal)&&IsScope(named.ContainingType));
    private static bool IsScalar(ITypeSymbol? type)=>type is not null&&(type.SpecialType is not SpecialType.None and not SpecialType.System_Object
        ||type.ContainingNamespace.ToDisplayString()=="System"&&type.Name is "Decimal" or "DateTime" or "Guid" or "TimeSpan");
    private void Visit(IOperation? operation){
        this.cancellationToken.ThrowIfCancellationRequested();
        if(operation is null){this.unsafeAccess=true;return;}
        if(--this.remaining<=0||operation.Kind is OperationKind.Invalid or OperationKind.DynamicInvocation or OperationKind.DynamicMemberReference){this.Unsafe(operation.Syntax,"不明: 解析上限、未解決操作、またはdynamic経路");return;}
        switch(operation){
            case IInvocationOperation call:this.Call(call.TargetMethod,call);break;
            case IObjectCreationOperation creation:
                if(creation.Constructor is null){this.unsafeAccess=true;}else{this.Method(creation.Constructor);}break;
            case IMethodReferenceOperation reference:this.Method(reference.Method);break;
            case IPropertyReferenceOperation property:
                if(IsView(property.Instance?.Type)||IsScope(property.Instance?.Type)){this.Note(property.Syntax,"行/取引ビュー参照: "+property.Syntax+"。この参照だけでは物理走査しない");}
                else if(property.Property.GetMethod is { } getter){this.Method(getter);}break;
            case IAssignmentOperation assignment:
                // [関係代入] 表のプロパティへの代入(tx.dbo.T = …)は、差分を書く書き込み。
                if(assignment.Target is IPropertyReferenceOperation{Type:var assigned}&&IsView(assigned)){this.Unsafe(assignment.Syntax,"書込み: 関係代入 "+assignment.Target.Syntax+"。差分を確定まで私有化");}
                if(assignment.Target is IPropertyReferenceOperation target&&IsView(target.Instance?.Type)){this.Unsafe(assignment.Syntax,"書込み: "+assignment.Target.Syntax+"。キーは実行時に依存記録し、変更を確定まで私有化");}
                if(assignment.Target is not ILocalReferenceOperation&&(IsView(assignment.Value.Type)||IsScope(assignment.Value.Type))){this.Unsafe(assignment.Syntax,"不明: 取引または行ビューがローカル変数の外へ渡る");}break;
            case IIncrementOrDecrementOperation increment:
                if(increment.Target is IPropertyReferenceOperation incrementTarget&&IsView(incrementTarget.Instance?.Type)){this.Unsafe(increment.Syntax,"書込み: 行ビューへの増減演算");}
                if(increment.OperatorMethod is { } incrementMethod){this.Method(incrementMethod);}break;
            case IBinaryOperation{OperatorMethod:{ } binary}:this.Method(binary);break;
            case IUnaryOperation{OperatorMethod:{ } unary}:this.Method(unary);break;
            case IConversionOperation{OperatorMethod:{ } conversion}:this.Method(conversion);break;
            case IUsingOperation resource:this.Disposal(resource.Resources.Type,resource.Resources);break;
            case IUsingDeclarationOperation declaration:this.Disposal(null,declaration.DeclarationGroup);break;
            case IReturnOperation{ReturnedValue:{ } returned}:
                if(IsView(returned.Type)||IsScope(returned.Type)){this.unsafeAccess=true;}break;
            case IFieldReferenceOperation field when field.Field.IsStatic:
                this.TypeInitialization(field.Field.ContainingType);break;
        }
        foreach(var child in operation.ChildOperations){this.Visit(child);}
    }
    private void Call(IMethodSymbol method,IInvocationOperation operation){
        if(IsView(operation.Instance?.Type)){
            if(method.Name=="Iterate"){
                if(method.TypeArguments.FirstOrDefault() is not INamedTypeSymbol strategy){this.unsafeAccess=true;return;}
                var invoke=strategy.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();if(invoke is null){this.unsafeAccess=true;}else{this.Method(invoke);}return;
            }
            if(method.Name is not("TryGetValue" or "ContainsKey" or "Contains" or "GetEnumerator" or "Where" or "Union" or "Except" or "Intersect")){this.Unsafe(operation.Syntax,"書込み/不明: 行ビューの"+method.Name);}
            else{this.Note(operation.Syntax,"読取り: "+operation.Syntax+"。点検索はキー依存、列挙は表依存でsnapshotを選ぶ");}return;
        }
        if(IsScope(operation.Instance?.Type)){
            if(method.Name is not("BeginTransactionAsync" or "BeginNested" or "CommitAsync" or "RollbackAsync" or "DisposeAsync" or "CreateSavepoint" or "RollbackToSavepoint" or "ReleaseSavepoint")){this.Unsafe(operation.Syntax,"不明: 取引への"+method.Name);}
            else{this.Note(operation.Syntax,"取引制御: "+method.Name+"。子は同じ取引ID/snapshotへ合流し、外側だけが公開");}return;
        }
        var definition=method.ReducedFrom??method;
        if(definition.ContainingType.ToDisplayString() is "System.Linq.Enumerable" or "Seamless.Linq.Enumerable"){
            if(definition.Name is not("Sum" or "Count" or "LongCount" or "Any" or "Where" or "Select" or "First" or "FirstOrDefault" or "Single" or "SingleOrDefault" or "Skip" or "Take" or "ToArray" or "ToList" or "AsEnumerable")){this.Unsafe(operation.Syntax,"不明: "+definition.Name+"の比較器/コールバック効果はこの境界で確定できない");return;}
            this.Note(operation.Syntax,"読取り演算: "+definition.Name+"。callbackの効果も追跡し、並列化の安全性とは別に判定");
            var source=operation.Arguments.FirstOrDefault()?.Value;
            while(source is IConversionOperation conversion){source=conversion.Operand;}
            if(source is null||!KnownSource(source)){this.Unsafe(operation.Syntax,"不明: 列挙元のGetEnumerator/MoveNextの効果を確定できない");return;}
            foreach(var argument in operation.Arguments){
                if(argument.Value.Type?.TypeKind==TypeKind.Delegate&&argument.Value is not IDelegateCreationOperation){this.Unsafe(argument.Syntax,"不明: delegate変数の呼出し先");}
            }
            return;
        }
        this.Method(method);
    }
    private static bool KnownSource(IOperation operation)=>IsView(operation.Type)||operation.Type is IArrayTypeSymbol
        ||operation.Type is INamedTypeSymbol type&&type.OriginalDefinition.ToDisplayString()=="System.Collections.Generic.List<T>"
        ||operation is IInvocationOperation invocation&&(invocation.TargetMethod.ReducedFrom??invocation.TargetMethod).ContainingType.ToDisplayString() is "System.Linq.Enumerable" or "Seamless.Linq.Enumerable";
    private void Method(IMethodSymbol method){
        if(this.remaining<=0){return;}
        if(method.MethodKind==MethodKind.DelegateInvoke||method.IsAbstract||method.IsVirtual&&!method.IsSealed||method.IsOverride&&!method.IsSealed){this.unsafeAccess=true;this.steps.Add("[アクセス] 不明: 動的呼出し先 "+Clean(method.ToDisplayString()));return;}
        var type=method.ContainingType;var ns=type.ContainingNamespace.ToDisplayString();
        if(ns=="System"&&(IsScalar(type)||type.Name is "Object" or "Exception" or "InvalidOperationException" or "ArgumentException")
            ||ns=="System.Threading"&&type.Name=="CancellationToken"
            ||ns=="System.Threading.Tasks"&&type.Name is "Task" or "ValueTask"
            ||ns=="System.Runtime.CompilerServices"&&type.Name.EndsWith("Awaitable",StringComparison.Ordinal)){return;}
        if(method.IsImplicitlyDeclared&&method.MethodKind is MethodKind.PropertyGet or MethodKind.Constructor&&method.DeclaringSyntaxReferences.Length==0){
            if(type.DeclaringSyntaxReferences.Length==0){this.unsafeAccess=true;}else{this.TypeInitialization(type);}return;
        }
        if(method.DeclaringSyntaxReferences.Length==0||!this.visiting.Add(method.OriginalDefinition)){this.unsafeAccess=true;this.steps.Add("[アクセス] 不明: 本体なし/循環呼出し "+Clean(method.ToDisplayString()));return;}
        try{
            if(method.MethodKind==MethodKind.Constructor){this.TypeInitialization(type);}
            foreach(var reference in method.DeclaringSyntaxReferences){
                var syntax=reference.GetSyntax(this.cancellationToken);var model=this.compilation.GetSemanticModel(syntax.SyntaxTree);
                this.Note(syntax,"既知の呼出し先を合成: "+method.ToDisplayString());
                var methodBody=model.GetOperation(syntax,this.cancellationToken);
                if(methodBody is IMethodBodyOperation or IConstructorBodyOperation){this.Visit(methodBody);continue;}
                if(syntax is ConstructorDeclarationSyntax{Initializer:{ } initializer}){this.Visit(model.GetOperation(initializer,this.cancellationToken));}
                SyntaxNode? body=syntax switch{
                    MethodDeclarationSyntax declaration=>declaration.Body??(SyntaxNode?)declaration.ExpressionBody?.Expression,
                    ConstructorDeclarationSyntax declaration=>declaration.Body??(SyntaxNode?)declaration.ExpressionBody?.Expression,
                    LocalFunctionStatementSyntax declaration=>declaration.Body??(SyntaxNode?)declaration.ExpressionBody?.Expression,
                    AccessorDeclarationSyntax declaration=>declaration.Body??(SyntaxNode?)declaration.ExpressionBody?.Expression,
                    PropertyDeclarationSyntax declaration=>declaration.ExpressionBody?.Expression,
                    _=>null,
                };
                if(body is null){
                    if(syntax is AccessorDeclarationSyntax{SemicolonToken.RawKind:(int)SyntaxKind.SemicolonToken}||syntax is ParameterSyntax){continue;}
                    this.unsafeAccess=true;return;
                }
                while(body is ParenthesizedExpressionSyntax or CheckedExpressionSyntax){
                    if(body is ParenthesizedExpressionSyntax parenthesized){body=parenthesized.Expression;}
                    else{body=((CheckedExpressionSyntax)body).Expression;}
                }
                var operation=model.GetOperation(body,this.cancellationToken);
                if(operation is null){this.Unsafe(body,"不明: 呼出し先の本体に対応するoperationが得られない");}
                else{this.Visit(operation);}
            }
        }finally{this.visiting.Remove(method.OriginalDefinition);}
    }
    private void TypeInitialization(INamedTypeSymbol type){
        var ns=type.ContainingNamespace.ToDisplayString();if(type.DeclaringSyntaxReferences.Length==0&&(ns=="System"||ns.StartsWith("System.",StringComparison.Ordinal))){return;}
        if(!this.initializing.Add(type)){this.unsafeAccess=true;return;}
        try{
        foreach(var initializer in type.StaticConstructors.Where(static method=>!method.IsImplicitlyDeclared)){this.Method(initializer);}
        foreach(var reference in type.DeclaringSyntaxReferences){
            if(reference.GetSyntax(this.cancellationToken) is not TypeDeclarationSyntax declaration){continue;}
            var model=this.compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach(var field in declaration.Members.OfType<FieldDeclarationSyntax>()){
                foreach(var variable in field.Declaration.Variables){if(variable.Initializer is { } initializer){this.Visit(model.GetOperation(initializer.Value,this.cancellationToken));}}
            }
            if(type.BaseType is {SpecialType:SpecialType.None}){this.unsafeAccess=true;}
        }
        }finally{this.initializing.Remove(type);}
    }
    private void Disposal(ITypeSymbol? type,IOperation resources){
        if(type is null){
            foreach(var child in resources.ChildOperations){
                if(child is IVariableDeclarationOperation declaration){foreach(var variable in declaration.Declarators){this.Disposal(variable.Symbol.Type,variable);}}
                else{this.Disposal(child.Type,child);}
            }
            return;
        }
        if(IsScope(type)){return;}
        if(type is not INamedTypeSymbol named){this.unsafeAccess=true;return;}
        var disposal=named.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(static method=>method.Name is "Dispose" or "DisposeAsync");
        if(disposal is null){this.unsafeAccess=true;}else{this.Method(disposal);}
    }
}
