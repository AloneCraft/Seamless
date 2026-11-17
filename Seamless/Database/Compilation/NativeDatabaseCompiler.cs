using Microsoft.CodeAnalysis.Text;
using Seamless.Database.Rdbms;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Database.Compilation;

/// <summary>ソースを実行せず、解決済みシンボルからDBと読み取り処理を抽出する。</summary>
public sealed class NativeDatabaseCompiler{
    public NativeAnalysisResult Analyze(NativeCompilationSnapshot snapshot,string databaseTypeName,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentException.ThrowIfNullOrWhiteSpace(databaseTypeName);
        var compilation=BuildCompilation(snapshot,cancellationToken,out var generatorDiagnostics);
        var diagnostics=ImmutableArray.CreateBuilder<NativeDiagnostic>();
        foreach(var error in compilation.GetDiagnostics(cancellationToken).Concat(generatorDiagnostics).Where(d=>d.Severity==DiagnosticSeverity.Error)){
            diagnostics.Add(new(error.Id,error.GetMessage(),NativeDiagnosticSeverity.CompilationError,null,Location(error.Location)));
        }
        if(diagnostics.Count>0){return new(new(databaseTypeName,snapshot.Fingerprint,[],[]),diagnostics.ToImmutable());}
        var root=compilation.GetTypeByMetadataName(databaseTypeName);
        if(root is null||!Derives(root,"Seamless.Database.DatabaseContainer")){
            diagnostics.Add(new("NATIVE_DATABASE_TYPE","The selected type must derive from DatabaseContainer.",NativeDiagnosticSeverity.CompilationError,databaseTypeName,null));
            return new(new(databaseTypeName,snapshot.Fingerprint,[],[]),diagnostics.ToImmutable());
        }
        var binding=new Binding(compilation,diagnostics,cancellationToken);
        var types=new List<INamedTypeSymbol>{root};
        // 型ごとの、目録での名前の前置き(コンテナは空、スキーマは「スキーマ.」)。生成される IReader の表を同じ名前の表へ結び付けるのに使う。
        var prefixes=new Dictionary<INamedTypeSymbol,string>(SymbolEqualityComparer.Default){[root]=""};
        for(var index=0;index<types.Count;index++){
            foreach(var property in types[index].GetMembers().OfType<IPropertySymbol>().Where(p=>!p.IsStatic)){
                if(property.Type is INamedTypeSymbol schema&&Derives(schema,"Seamless.Database.DatabaseSchema")&&prefixes.TryAdd(schema,prefixes[types[index]]+property.Name+".")){types.Add(schema);}
            }
        }
        foreach(var type in types){
            foreach(var property in type.GetMembers().OfType<IPropertySymbol>()){
                if(property.Type is INamedTypeSymbol objectType&&objectType.ContainingNamespace.ToDisplayString()=="Seamless.Database"&&objectType.Name is "MaterializedView" or "StoredProcedure"){
                    diagnostics.Add(new("NATIVE_UNSUPPORTED_OBJECT","SQL conversion is not implemented for "+objectType.Name+".",NativeDiagnosticSeverity.Unsupported,property.Name,Location(property.Locations.First())));continue;
                }
                if(property.Type is not INamedTypeSymbol tableType||tableType.ContainingNamespace.ToDisplayString()!="Seamless.Database"||tableType.Name!="Table"){continue;}
                try{binding.AddTable(property,tableType,prefixes[type]+property.Name);}catch(Unsupported error){binding.Report(property.Name,error);}
            }
        }
        var queries=ImmutableArray.CreateBuilder<NativeQueryDefinition>();
        var sourcePaths=snapshot.Sources.Select(s=>s.Path).ToHashSet(StringComparer.Ordinal);
        foreach(var type in types){
            foreach(var method in type.GetMembers().OfType<IMethodSymbol>().Where(m=>m.MethodKind==MethodKind.Ordinary&&m.DeclaredAccessibility==Accessibility.Public&&!m.IsStatic)){
                var declaration=method.DeclaringSyntaxReferences.Where(r=>sourcePaths.Contains(r.SyntaxTree.FilePath)).Select(r=>r.GetSyntax(cancellationToken)).OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if(declaration is null||method.Name is "Dispose"||method.ReturnsVoid){continue;}
                try{
                    var query=binding.Query(binding.Body(declaration),new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
                    var parameters=method.Parameters.Select(p=>new NativeParameter(p.Name,TypeName(p.Type),IsNullable(p.Type,p.NullableAnnotation))).ToImmutableArray();
                    queries.Add(new(method.Name,Schema(type),parameters,query,Location(declaration.GetLocation())!));
                }catch(Unsupported error){binding.Report(method.Name,error);}
            }
        }
        // ビュー・関数は、生成される入口の共通インターフェース(IReader、スキーマは I<スキーマ>Reader)への拡張メンバーとして書く。
        var readers=new Dictionary<INamedTypeSymbol,INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach(var type in types){if(root.GetTypeMembers(ReaderInterface(prefixes[type])).FirstOrDefault(static item=>item.TypeKind==TypeKind.Interface) is{} reader){readers[reader]=type;binding.ReaderPrefixes[reader]=prefixes[type];}}
        foreach(var extension in ExtensionBlocks(compilation,sourcePaths,cancellationToken)){
            if(extension.ExtensionParameter?.Type is not INamedTypeSymbol receiver||!readers.TryGetValue(receiver,out var owner)){continue;}
            foreach(var member in extension.GetMembers().Where(m=>!m.IsStatic&&m.DeclaredAccessibility==Accessibility.Public)){
                var syntax=member.DeclaringSyntaxReferences.Where(r=>sourcePaths.Contains(r.SyntaxTree.FilePath)).Select(r=>r.GetSyntax(cancellationToken)).FirstOrDefault();
                ExpressionSyntax body;ImmutableArray<NativeParameter> parameters=[];
                try{
                    if(member is IPropertySymbol{IsIndexer:false} property&&syntax is PropertyDeclarationSyntax propertySyntax){body=binding.Body(propertySyntax);}
                    else if(member is IMethodSymbol{MethodKind:MethodKind.Ordinary,ReturnsVoid:false} method&&syntax is MethodDeclarationSyntax methodSyntax){
                        body=binding.Body(methodSyntax);
                        parameters=method.Parameters.Select(p=>new NativeParameter(p.Name,TypeName(p.Type),IsNullable(p.Type,p.NullableAnnotation))).ToImmutableArray();
                    }else{continue;}
                    var query=binding.Query(body,new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
                    queries.Add(new(member.Name,Schema(owner),parameters,query,Location(syntax.GetLocation())!));
                }catch(Unsupported error){binding.Report(member.Name,error);}
            }
        }
        return new(new(root.Name,snapshot.Fingerprint,binding.Tables.ToImmutableArray(),queries.ToImmutable()),diagnostics.ToImmutable());
    }

    /// <summary>ネイティブ実行用アセンブリを生成する。読み込み・実行・比較は呼出し側の責務。</summary>
    public NativeAssemblyArtifact Emit(NativeCompilationSnapshot snapshot,CancellationToken cancellationToken=default){
        var compilation=BuildCompilation(snapshot,cancellationToken,out var generatorDiagnostics);
        using var stream=new MemoryStream();var result=compilation.Emit(stream,cancellationToken:cancellationToken);
        var diagnostics=result.Diagnostics.Concat(generatorDiagnostics).Where(d=>d.Severity==DiagnosticSeverity.Error).Select(d=>new NativeDiagnostic(d.Id,d.GetMessage(),NativeDiagnosticSeverity.CompilationError,null,Location(d.Location))).ToImmutableArray();
        ImmutableArray<byte>bytes=[];
        if(result.Success&&diagnostics.Length==0){bytes=ImmutableArray.CreateRange(stream.ToArray());}
        return new(snapshot.Fingerprint,bytes,diagnostics);
    }

    private static CSharpCompilation BuildCompilation(NativeCompilationSnapshot snapshot,CancellationToken cancellationToken,out ImmutableArray<Diagnostic>generatorDiagnostics){
        ArgumentNullException.ThrowIfNull(snapshot);
        var parse=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:snapshot.Defines);
        var trees=snapshot.Sources.Select(s=>CSharpSyntaxTree.ParseText(s.Content,parse,s.Path,cancellationToken:cancellationToken)).ToArray();
        var compilation=CSharpCompilation.Create("SeamlessAnalysis_"+snapshot.Fingerprint,trees,snapshot.ReferenceImages.Select(i=>MetadataReference.CreateFromImage(i)),new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,checkOverflow:snapshot.CheckOverflow,nullableContextOptions:NullableContextOptions.Enable,optimizationLevel:OptimizationLevel.Release,deterministic:true));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Shared.Database.Generation.DatabaseSchemaGeneration().AsSourceGenerator(),new global::Shared.Database.Generation.DatabaseContainerGeneration().AsSourceGenerator()],parseOptions:parse);
        driver.RunGeneratorsAndUpdateCompilation(compilation,out var updated,out generatorDiagnostics,cancellationToken);
        return (CSharpCompilation)updated;
    }

    // DB生成器の入口のインターフェース名(Shared/Database.Generation の ReaderInterface と同じ規則)。
    private static string ReaderInterface(string prefix)=>prefix.Length==0?"IReader":"I"+string.Concat(prefix.TrimEnd('.').Split('.'))+"Reader";
    private static IEnumerable<INamedTypeSymbol> ExtensionBlocks(CSharpCompilation compilation,HashSet<string> sourcePaths,CancellationToken cancellationToken){
        var seen=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach(var tree in compilation.SyntaxTrees.Where(tree=>sourcePaths.Contains(tree.FilePath))){
            var model=compilation.GetSemanticModel(tree);
            foreach(var declaration in tree.GetRoot(cancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>()){
                if(model.GetDeclaredSymbol(declaration,cancellationToken) is not{IsStatic:true} type){continue;}
                foreach(var extension in type.GetTypeMembers().Where(static item=>item.IsExtension)){if(seen.Add(extension)){yield return extension;}}
            }
        }
    }
    private static bool Derives(INamedTypeSymbol type,string name){for(var current=type;current is not null;current=current.BaseType){if(current.ToDisplayString()==name){return true;}}return false;}
    private static string TypeName(ITypeSymbol type){if(type is INamedTypeSymbol n&&n.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T){type=n.TypeArguments[0];}return type.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle:SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));}
    private static bool IsNullable(ITypeSymbol type,NullableAnnotation annotation)=>annotation==NullableAnnotation.Annotated||(type is INamedTypeSymbol n&&n.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T);
    private static string Schema(INamedTypeSymbol type){
        foreach(var syntax in type.DeclaringSyntaxReferences.Select(r=>r.GetSyntax())){
            var baseType=syntax.DescendantNodes().OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault();
            if(baseType?.ArgumentList.Arguments.LastOrDefault()?.Expression is LiteralExpressionSyntax literal&&literal.IsKind(SyntaxKind.StringLiteralExpression)){return literal.Token.ValueText;}
        }
        return "dbo";
    }
    internal static NativeSourceLocation?Location(Location location){if(!location.IsInSource){return null;}var line=location.GetLineSpan();return new(line.Path,location.SourceSpan.Start,location.SourceSpan.Length,line.StartLinePosition.Line+1,line.StartLinePosition.Character+1);}

    private sealed class Unsupported(string code,string message,SyntaxNode node):Exception(message){public string Code{get;}=code;public SyntaxNode Node{get;}=node;}

    private sealed class Binding(CSharpCompilation compilation,ImmutableArray<NativeDiagnostic>.Builder diagnostics,CancellationToken cancellationToken){
        private readonly Dictionary<IPropertySymbol,NativeTable>tables=new(SymbolEqualityComparer.Default);
        private readonly Dictionary<string,NativeTable>tablesByPath=new(StringComparer.Ordinal);
        // 生成される入口のインターフェース(IReader・I<スキーマ>Reader)と、目録での名前の前置き。ビュー・関数の本体の q.X を表へ結び付ける。
        public Dictionary<INamedTypeSymbol,string> ReaderPrefixes{get;}=new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ISymbol,string>columns=new(SymbolEqualityComparer.Default);
        public IEnumerable<NativeTable>Tables=>this.tables.Values;
        private SemanticModel Model(SyntaxNode node)=>compilation.GetSemanticModel(node.SyntaxTree);
        private ISymbol?Symbol(SyntaxNode node)=>this.Model(node).GetSymbolInfo(node,cancellationToken).Symbol;
        public void Report(string name,Unsupported error)=>diagnostics.Add(new(error.Code,error.Message,NativeDiagnosticSeverity.Unsupported,name,Location(error.Node.GetLocation())));

        // Tables と [Key] で書いた行の生成された Key(IRecord<キーの型> の Key)。それ以外の行では null。
        private static IPropertySymbol? RecordKey(INamedTypeSymbol record){
            var contract=record.AllInterfaces.FirstOrDefault(static item=>item.OriginalDefinition.ToDisplayString()=="Seamless.Collections.IRecord<TKey>");
            if(contract is null){return null;}
            return record.GetMembers("Key").OfType<IPropertySymbol>().FirstOrDefault(key=>!key.IsStatic&&SymbolEqualityComparer.Default.Equals(key.Type,contract.TypeArguments[0]));
        }

        public void AddTable(IPropertySymbol property,INamedTypeSymbol tableType,string path){
            var record=(INamedTypeSymbol)tableType.TypeArguments[^1];
            // [行のキー] Tables と [Key] で書いた行は、生成された Key(キーの型)を持つ。Key は列ではなく、キーの型の列(行の同じ名前の列)が主キー。
            var recordKey=RecordKey(record);
            var columns=record.GetMembers().Where(m=>!m.IsStatic&&!SymbolEqualityComparer.Default.Equals(m,recordKey)&&((m is IPropertySymbol p&&!p.IsIndexer&&p.DeclaredAccessibility==Accessibility.Public)||(m is IFieldSymbol f&&!f.IsImplicitlyDeclared&&f.DeclaredAccessibility==Accessibility.Public))).Select(m=>{
                ITypeSymbol type;NullableAnnotation nullable;
                if(m is IPropertySymbol p){type=p.Type;nullable=p.NullableAnnotation;}else{var f=(IFieldSymbol)m;type=f.Type;nullable=f.NullableAnnotation;}
                var capacity=TextCapacity(type);
                var imported=m.GetAttributes().FirstOrDefault(a=>a.AttributeClass?.ToDisplayString()=="Seamless.Database.ImportedDatabaseColumnAttribute");
                if(imported?.NamedArguments.Any(a=>a.Key=="IsSynthetic"&&a.Value.Value is true)==true){return null;}
                var name=imported?.ConstructorArguments[0].Value as string??m.Name;
                this.columns[m]=name;
                int?logicalLength=null;
                if((capacity is not null||type.SpecialType==SpecialType.System_String)&&imported?.ConstructorArguments[1].Value is int maximum&&maximum>0){logicalLength=maximum;}
                return new NativeColumn(name,TypeName(type),IsNullable(type,nullable),capacity){MemberName=m.Name,LogicalMaximumLength=logicalLength,SourceSqlTypeName=imported?.ConstructorArguments[2].Value as string,SourceCollation=imported?.ConstructorArguments[3].Value as string};
            }).OfType<NativeColumn>().ToImmutableArray();
            if(columns.Length==0){throw new Unsupported("NATIVE_TABLE_COLUMNS","The table record has no public columns.",property.DeclaringSyntaxReferences[0].GetSyntax());}
            var keys=ImmutableArray.CreateBuilder<string>();
            if(tableType.TypeArguments.Length==2){
                var syntax=property.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
                var lambda=syntax.DescendantNodes().OfType<LambdaExpressionSyntax>().FirstOrDefault();
                if(lambda is null){throw new Unsupported("NATIVE_PRIMARY_KEY","A source key selector is required.",syntax);}
                IEnumerable<ExpressionSyntax>parts;
                if(lambda.Body is BaseObjectCreationExpressionSyntax creation&&creation.ArgumentList is not null){parts=creation.ArgumentList.Arguments.Select(a=>a.Expression);}
                else if(lambda.Body is MemberAccessExpressionSyntax direct){parts=[direct];}
                else{throw new Unsupported("NATIVE_PRIMARY_KEY","Only direct record-column key selectors are supported.",lambda);}
                var synthetic=false;
                foreach(var part in parts){
                    if(part is not MemberAccessExpressionSyntax member||this.Symbol(member) is not{}symbol){throw new Unsupported("NATIVE_PRIMARY_KEY","Computed primary keys require an explicit SQL expression mapping.",part);}
                    if(recordKey is not null&&SymbolEqualityComparer.Default.Equals(symbol,recordKey)){
                        // 生成された表のプロパティの row=>row.Key。キーの型の列と同じ名前の行の列が主キー。
                        foreach(var keyColumn in recordKey.Type.GetMembers().OfType<IPropertySymbol>().Where(static p=>!p.IsStatic&&p.DeclaredAccessibility==Accessibility.Public)){
                            var column=record.GetMembers(keyColumn.Name).OfType<IPropertySymbol>().FirstOrDefault();
                            if(column is not null&&column.GetAttributes().Any(a=>a.AttributeClass?.ToDisplayString()=="Seamless.Database.ImportedDatabaseColumnAttribute"&&a.NamedArguments.Any(v=>v.Key=="IsSynthetic"&&v.Value.Value is true))){synthetic=true;continue;}
                            if(column is null||!this.columns.TryGetValue(column,out var name)){throw new Unsupported("NATIVE_PRIMARY_KEY","The generated key column is not an exported column.",part);}
                            keys.Add(name);
                        }
                        continue;
                    }
                    if(this.columns.TryGetValue(symbol,out var key)){keys.Add(key);}
                    else if(symbol.GetAttributes().Any(a=>a.AttributeClass?.ToDisplayString()=="Seamless.Database.ImportedDatabaseColumnAttribute"&&a.NamedArguments.Any(v=>v.Key=="IsSynthetic"&&v.Value.Value is true))){synthetic=true;}
                    else{throw new Unsupported("NATIVE_PRIMARY_KEY","The key selector does not reference an exported column.",part);}
                }
                if(keys.Count==0&&!synthetic){throw new Unsupported("NATIVE_PRIMARY_KEY","The key selector cannot be reduced to record columns.",lambda);}
            }
            var origin=property.GetAttributes().FirstOrDefault(a=>a.AttributeClass?.ToDisplayString()=="Seamless.Database.ImportedDatabaseObjectAttribute");
            this.tables.Add(property,new(origin?.ConstructorArguments[0].Value as string??Schema(property.ContainingType),origin?.ConstructorArguments[1].Value as string??property.Name,property.ToDisplayString(),columns,keys.Distinct(StringComparer.Ordinal).ToImmutableArray()){
                SourceFeatures=origin?.ConstructorArguments.ElementAtOrDefault(2).Value as string,
            });
            this.tablesByPath[path]=this.tables[property];
        }

        private static int?TextCapacity(ITypeSymbol type){
            if(type is INamedTypeSymbol nullable&&nullable.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T){type=nullable.TypeArguments[0];}
            if(type is not INamedTypeSymbol named||named.Name!="FixedText"||named.ContainingNamespace.ToDisplayString()!="Seamless.Collections"){return null;}
            var attribute=named.TypeArguments[0].GetAttributes().FirstOrDefault(a=>a.AttributeClass?.ToDisplayString()=="System.Runtime.CompilerServices.InlineArrayAttribute");
            if(attribute?.ConstructorArguments[0].Value is int capacity){return capacity;}
            return null;
        }

        public ExpressionSyntax Body(MethodDeclarationSyntax method){
            if(method.ExpressionBody is not null){return method.ExpressionBody.Expression;}
            if(method.Body?.Statements is [ReturnStatementSyntax{Expression:{}expression}]){return expression;}
            throw new Unsupported("NATIVE_UNSUPPORTED_BODY","This method body is not yet supported for SQL generation.",method);
        }
        public ExpressionSyntax Body(PropertyDeclarationSyntax property){
            if(property.ExpressionBody is not null){return property.ExpressionBody.Expression;}
            var getter=property.AccessorList?.Accessors.SingleOrDefault(static accessor=>accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
            if(getter?.ExpressionBody is not null){return getter.ExpressionBody.Expression;}
            if(getter?.Body?.Statements is [ReturnStatementSyntax{Expression:{}expression}]){return expression;}
            throw new Unsupported("NATIVE_UNSUPPORTED_BODY","This property body is not yet supported for SQL generation.",property);
        }

        public NativeQuery Query(ExpressionSyntax expression,HashSet<IMethodSymbol>stack){
            if(expression is ParenthesizedExpressionSyntax parenthesized){return this.Query(parenthesized.Expression,stack);}
            if(this.Symbol(expression) is IPropertySymbol property){
                if(this.tables.TryGetValue(property,out var table)){return new(table,[]);}
                // ビュー・関数の本体の q.X(生成される入口の表)は、同じ名前の表。
                if(this.ReaderPrefixes.TryGetValue(property.ContainingType,out var prefix)&&this.tablesByPath.TryGetValue(prefix+property.Name,out var readerTable)){return new(readerTable,[]);}
                // 別のビュー(拡張プロパティ)の参照は、その本体を展開する。
                if(property.ContainingType.IsExtension&&property.GetMethod is{} getter&&property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is PropertyDeclarationSyntax view){
                    if(!stack.Add(getter)){throw new Unsupported("NATIVE_RECURSIVE_QUERY","Recursive source expansion is unsupported.",expression);}
                    try{return this.Query(this.Body(view),stack);}finally{stack.Remove(getter);}
                }
            }
            if(expression is not InvocationExpressionSyntax invocation||this.Symbol(invocation) is not IMethodSymbol method){throw new Unsupported("NATIVE_UNSUPPORTED_QUERY","Expected a table or a supported LINQ call.",expression);}
            var owner=(method.ReducedFrom??method).ContainingType.ToDisplayString();
            // Seamless.Linq.Set は集合の演算で、Select は結果の重複を除く(SELECT DISTINCT)。それ以外は System.Linq と同じ。
            var setOperation=owner=="Seamless.Linq.Set";
            // [関係代入] 表の型(Seamless.Database.Table)が自分で持つ Where は、System.Linq の Where と同じ結果になる。
            var tableOperation=method is{IsStatic:false,ReducedFrom:null,Name:"Where"}&&method.ContainingType.OriginalDefinition.Name=="Table"&&method.ContainingType.OriginalDefinition.ContainingNamespace.ToDisplayString()=="Seamless.Database";
            if(owner is "System.Linq.Enumerable" or "Seamless.Linq.Enumerable"||setOperation&&method.Name is "Where" or "Select" or "Distinct"||tableOperation){
                var arguments=invocation.ArgumentList.Arguments.Select(a=>a.Expression).ToArray();
                ExpressionSyntax input;
                if((method.ReducedFrom is not null||tableOperation)&&invocation.Expression is MemberAccessExpressionSyntax member){input=member.Expression;}
                else{if(arguments.Length==0){throw new Unsupported("NATIVE_UNSUPPORTED_QUERY","LINQ input is missing.",invocation);}input=arguments[0];arguments=arguments[1..];}
                var query=this.Query(input,stack);
                NativeQueryStage stage;
                switch(method.Name){
                    case "Where" when arguments.Length==1:stage=new NativeFilter(this.Lambda(arguments[0]));break;
                    case "Select" when arguments.Length==1:stage=new NativeSelect(this.Project(arguments[0]));break;
                    case "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" when arguments.Length==1:
                        stage=new NativeOrder(new(this.Lambda(arguments[0]),method.Name.EndsWith("Descending",StringComparison.Ordinal)),method.Name.StartsWith("Then",StringComparison.Ordinal));break;
                    case "Take" when arguments.Length==1&&this.Model(arguments[0]).GetConstantValue(arguments[0]).Value is int count:stage=new NativeTake(Math.Max(count,0));break;
                    case "Distinct" when arguments.Length==0:stage=new NativeDistinct();break;
                    case "Count" or "LongCount" or "Sum" or "Min" or "Max" or "Average" when arguments.Length<=1:
                        NativeExpression?operand=null;if(arguments.Length==1){operand=this.Lambda(arguments[0]);}
                        stage=new NativeAggregate(method.Name,operand);break;
                    default:throw new Unsupported("NATIVE_UNSUPPORTED_LINQ","Unsupported LINQ operation or overload: "+method.ToDisplayString(),invocation);
                }
                if(setOperation&&method.Name=="Select"){return query with{Stages=query.Stages.Add(stage).Add(new NativeDistinct())};}
                return query with{Stages=query.Stages.Add(stage)};
            }
            if(invocation.ArgumentList.Arguments.Count==0&&method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is MethodDeclarationSyntax helper){
                if(!stack.Add(method)){throw new Unsupported("NATIVE_RECURSIVE_QUERY","Recursive source expansion is unsupported.",invocation);}
                try{return this.Query(this.Body(helper),stack);}finally{stack.Remove(method);}
            }
            throw new Unsupported("NATIVE_UNSUPPORTED_CALL","The call cannot be translated: "+method.ToDisplayString(),invocation);
        }

        private NativeExpression Lambda(ExpressionSyntax expression){
            if(expression is not LambdaExpressionSyntax lambda||lambda.Body is not ExpressionSyntax body){throw new Unsupported("NATIVE_UNSUPPORTED_LAMBDA","Expected an expression lambda.",expression);}
            if(lambda is ParenthesizedLambdaExpressionSyntax p&&p.ParameterList.Parameters.Count!=1){throw new Unsupported("NATIVE_UNSUPPORTED_LAMBDA","Indexed lambdas are unsupported.",lambda);}
            return this.Expression(body);
        }

        private ImmutableArray<NativeProjection>Project(ExpressionSyntax expression){
            if(expression is not LambdaExpressionSyntax{Body:ExpressionSyntax body}){throw new Unsupported("NATIVE_UNSUPPORTED_PROJECTION","Expected a projection lambda.",expression);}
            if(body is AnonymousObjectCreationExpressionSyntax anonymous){return anonymous.Initializers.Select(i=>new NativeProjection(i.NameEquals?.Name.Identifier.ValueText??(i.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText??throw new Unsupported("NATIVE_UNSUPPORTED_PROJECTION","Projection name is missing.",i),this.Expression(i.Expression))).ToImmutableArray();}
            if(body is BaseObjectCreationExpressionSyntax creation&&this.Symbol(creation) is IMethodSymbol constructor){
                if(creation.Initializer is not null){throw new Unsupported("NATIVE_UNSUPPORTED_PROJECTION","Object initializers are not yet supported.",creation);}
                var arguments=creation.ArgumentList?.Arguments??default;
                return arguments.Select((a,i)=>new NativeProjection(constructor.ContainingType.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p=>p.Name.Equals(constructor.Parameters[i].Name,StringComparison.OrdinalIgnoreCase))?.Name??constructor.Parameters[i].Name,this.Expression(a.Expression))).ToImmutableArray();
            }
            return [new("Value",this.Expression(body))];
        }

        private NativeExpression Expression(ExpressionSyntax expression){
            var constant=this.Model(expression).GetConstantValue(expression,cancellationToken);
            if(constant.HasValue){return new NativeConstantExpression(constant.Value);}
            if(expression is ParenthesizedExpressionSyntax p){return this.Expression(p.Expression);}
            if(expression is IdentifierNameSyntax&&this.Symbol(expression) is IParameterSymbol parameter){return new NativeParameterExpression(parameter.Name);}
            if(expression is MemberAccessExpressionSyntax member&&this.Symbol(member) is IPropertySymbol or IFieldSymbol){
                if(this.Symbol(member.Expression) is IParameterSymbol){
                    if(!this.columns.TryGetValue(this.Symbol(member)!,out var name)){name=member.Name.Identifier.ValueText;}
                    return new NativeColumnExpression(name);
                }
            }
            if(expression is BinaryExpressionSyntax binary&&binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression){
                var type=this.Model(binary.Left).GetTypeInfo(binary.Left).Type;
                var isText=type?.SpecialType==SpecialType.System_String||(type is INamedTypeSymbol n&&n.Name=="FixedText"&&n.ContainingNamespace.ToDisplayString()=="Seamless.Collections");
                if(isText&&binary.Kind() is not(SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)){throw new Unsupported("NATIVE_UNSUPPORTED_TEXT_OPERATOR","Only ordinal text equality is supported at this stage.",binary);}
                return new NativeBinaryExpression(binary.OperatorToken.Text,this.Expression(binary.Left),this.Expression(binary.Right)){OrdinalText=isText};
            }
            if(expression is PrefixUnaryExpressionSyntax unary&&unary.Kind() is SyntaxKind.LogicalNotExpression or SyntaxKind.UnaryMinusExpression){return new NativeUnaryExpression(unary.OperatorToken.Text,this.Expression(unary.Operand));}
            if(expression is InvocationExpressionSyntax call){throw new Unsupported("NATIVE_UNSUPPORTED_CALL","Unsupported call: "+this.Symbol(call)?.ToDisplayString(),call);}
            throw new Unsupported("NATIVE_UNSUPPORTED_EXPRESSION","Unsupported native expression: "+expression.Kind(),expression);
        }
    }
}
