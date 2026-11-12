using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management.Server;

public sealed record CompiledSource(byte[] AssemblyBytes,BuildResult Result):IDisposable{
    private readonly byte[] image=AssemblyBytes;
    public byte[] AssemblyBytes{get=>this.ImportedBuild?.AssemblyBytes??this.image;init=>this.image=value;}
    internal CapturedSourceBuild? ImportedBuild{get;init;}
    internal QueryAuditMetadata? AuditMetadata{get;init;}
    internal CompiledSource Retain(){
        if(this.ImportedBuild is null){return this;}
        return new([],this.Result){ImportedBuild=this.ImportedBuild.Retain(),AuditMetadata=this.AuditMetadata};
    }
    public void Dispose()=>this.ImportedBuild?.Dispose();
    public object? InvokeStatic(string type,string member,object?[]? arguments=null){
        if(this.AssemblyBytes.Length==0){throw new ManagementException("Validation","コンパイルに失敗しています。");}
        var context=new ManagedLoadContext(imported:this.ImportedBuild);
        try{using var stream=new MemoryStream(this.AssemblyBytes);return context.LoadFromStream(stream).GetType(type,true)!.GetMethod(member,BindingFlags.Public|BindingFlags.Static)!.Invoke(null,arguments);}
        finally{context.Unload();}
    }
}

public sealed class RuntimeCompiler{
    public CompiledSource Compile(SourceSnapshot source,IReadOnlyList<byte[]>? dependencies=null,string? entryType=null,string? entryMember=null)=>this.CompileCore(source,dependencies,entryType,entryMember,null);
    internal CompiledSource CompileForGeneration(SourceSnapshot source,Guid generation){if(generation==Guid.Empty){throw new ArgumentException("A generation identity is required.",nameof(generation));}return this.CompileCore(source,null,null,null,generation.ToString("N"));}
    /// <summary>コンパイルと同じ準備でDB生成器とFLinq生成器を走らせ、利用者ファイルと生成ファイルを返す。ユーザーコードの実行とEmitは行わない。</summary>
    /// <remarks>方針違反や生成器の診断があっても、得られた生成ファイルはそのまま返す。区分は利用者・DB生成器・FLinq生成器。</remarks>
    public GeneratedSourceFile[] GenerateSources(SourceSnapshot source){
        var prepared=this.Prepare(source,null,null);
        var files=prepared.Source.Files.Select(static file=>new GeneratedSourceFile("User",file.Path,file.Text)).ToList();
        if(prepared.Policy.Length>0){return files.ToArray();}
        var schemaCompilation=RunSchemaGenerators(prepared.Compilation,prepared.Parse,out _);
        files.AddRange(schemaCompilation.SyntaxTrees.Except(prepared.Compilation.SyntaxTrees).Select(static tree=>new GeneratedSourceFile("Database",GeneratedPath(tree),tree.GetText().ToString())));
        var output=RunLinqGenerator(prepared.Compilation,prepared.Parse,out _);
        files.AddRange(output.SyntaxTrees.Except(prepared.Compilation.SyntaxTrees).Select(static tree=>new GeneratedSourceFile("FLinq",GeneratedPath(tree),tree.GetText().ToString())));
        return files.ToArray();
    }
    private static string GeneratedPath(SyntaxTree tree)=>tree.FilePath.Replace('\\','/').Split('/').Last();
    private sealed record PreparedCompilation(SourceSnapshot Source,string Hash,CSharpParseOptions Parse,CSharpCompilation Compilation,SyntaxTree[] Trees,DiagnosticInfo[] Policy);
    private PreparedCompilation Prepare(SourceSnapshot source,IReadOnlyList<byte[]>? dependencies,string? generation){
        source=source.Freeze();SourceEditor.Validate(source);var hash=source.Hash();var name="Seamless.Managed_"+hash[..24];
        if(generation is not null){name+="_g"+generation;}
        var references=SourceEditor.PlatformReferences().ToList();
        var paths=references.OfType<PortableExecutableReference>().Select(reference=>reference.FilePath!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        void Add(Assembly assembly){if(assembly.IsDynamic||string.IsNullOrEmpty(assembly.Location)||!paths.Add(assembly.Location)){return;}references.Add(MetadataReference.CreateFromFile(assembly.Location));foreach(var item in assembly.GetReferencedAssemblies()){Add(Assembly.Load(item));}}
        Add(typeof(global::Seamless.Database.DatabaseContainer).Assembly);Add(typeof(ManagementExecutionContext).Assembly);Add(typeof(global::Seamless.Linq.Enumerable).Assembly);
        foreach(var reference in source.References){var path=paths.SingleOrDefault(path=>Path.GetFileNameWithoutExtension(path)==reference.Name);if(path is null||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!=reference.Sha256){throw new ManagementException("ReferenceMismatch","許可済み参照と一致しません: "+reference.Name);}}
        foreach(var dependency in dependencies??[]){references.Add(MetadataReference.CreateFromImage(dependency));}
        var parse=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:source.Defines??[]).WithFeatures([new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        var trees=source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,parse,file.Path)).ToArray();
        var compilation=CSharpCompilation.Create(name,trees,references,new(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release,allowUnsafe:true,nullableContextOptions:NullableContextOptions.Enable,deterministic:true));
        var policy=ValidatePolicy(compilation,trees);if(policy.Length>0){return new(source,hash,parse,compilation,trees,policy);}
        var companion=new System.Text.StringBuilder();
        foreach(var tree in trees){var model=compilation.GetSemanticModel(tree);foreach(var declaration in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Where(node=>node.Identifier.ValueText=="DatabaseGenerator")){
            if(model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol marker||marker.GetMembers("Collectible").Length>0){continue;}
            var ns=marker.ContainingNamespace.ToDisplayString();if(ns.Length>0){companion.Append("namespace ").Append(ns).Append('{');}
            companion.Append("internal static partial class DatabaseGenerator{internal const bool Collectible=true;}");if(ns.Length>0){companion.Append('}');}
        }}
        if(companion.Length>0){compilation=compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(companion.ToString(),parse,"ManagementCollectible.g.cs"));}
        return new(source,hash,parse,compilation,trees,[]);
    }
    private static Compilation RunSchemaGenerators(CSharpCompilation compilation,CSharpParseOptions parse,out System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics){
        GeneratorDriver schema=CSharpGeneratorDriver.Create([new global::Seamless.Database.Generator.DatabaseSchemaGenerator().AsSourceGenerator(),new global::Seamless.Database.Generator.DatabaseContainerGenerator().AsSourceGenerator()],parseOptions:parse);
        schema.RunGeneratorsAndUpdateCompilation(compilation,out var schemaCompilation,out diagnostics);
        return schemaCompilation;
    }
    private static Compilation RunLinqGenerator(Compilation compilation,CSharpParseOptions parse,out System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics){
        GeneratorDriver linq=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:parse);
        // FLinqの解析はDBスキーマを内部生成する。生成済み部分を渡すと同じメンバーが重複し、DB内の呼び出しを束縛できない。
        linq.RunGeneratorsAndUpdateCompilation(compilation,out var output,out diagnostics);
        return output;
    }
    private CompiledSource CompileCore(SourceSnapshot source,IReadOnlyList<byte[]>? dependencies,string? entryType,string? entryMember,string? generation){
        var prepared=this.Prepare(source,dependencies,generation);var hash=prepared.Hash;var parse=prepared.Parse;
        if(prepared.Policy.Length>0){return new([],new(hash,"",prepared.Policy,[]));}
        Compilation compilation=prepared.Compilation;
        var schemaCompilation=RunSchemaGenerators(prepared.Compilation,parse,out var schemaDiagnostics);
        IMethodSymbol? auditEntry=null;if(entryType is not null){
            var methods=schemaCompilation.GetTypeByMetadataName(entryType)?.GetMembers(entryMember??"").OfType<IMethodSymbol>().Where(method=>method.IsStatic&&method.DeclaredAccessibility==Accessibility.Public&&!method.IsGenericMethod&&method.Parameters.All(parameter=>parameter.RefKind==RefKind.None)).ToArray()??[];
            if(methods.Length!=1){return new([],new(hash,"",[new("ManagementEntry","公開された一意のstatic入口を指定してください。")],[]));}
            var method=methods[0];auditEntry=method;var signature=string.Join(",",method.Parameters.Select(parameter=>parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+" @"+parameter.Name));var arguments=string.Join(",",method.Parameters.Select(parameter=>"@"+parameter.Name));
            var call="global::"+entryType+".@"+method.Name+"("+arguments+")";var body="return "+call+";";var resultType=method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if(method.ReturnsVoid){resultType="object";body=call+";return null!;";}
            var bridge="namespace Seamless.Management.Generated{public static class QueryEntry{public static "+resultType+" Invoke("+signature+"){"+body+"}}}";
            var bridgeTree=CSharpSyntaxTree.ParseText(bridge,parse,"ManagementEntry.g.cs");
            compilation=compilation.AddSyntaxTrees(bridgeTree);schemaCompilation=schemaCompilation.AddSyntaxTrees(bridgeTree);
        }
        var output=RunLinqGenerator(compilation,parse,out var linqDiagnostics);
        output=output.AddSyntaxTrees(schemaCompilation.SyntaxTrees.Except(compilation.SyntaxTrees));
        using var pe=new MemoryStream();var emit=output.Emit(pe);
        var diagnostics=schemaDiagnostics.Concat(linqDiagnostics).Concat(emit.Diagnostics).Where(item=>item.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).Distinct().Select(SourceEditor.ToDiagnostic).Select(item=>{if(item.Code is "FLINQ002" or "CS8785"){return item with{Severity="Error"};}return item;}).ToArray();
        if(!emit.Success||diagnostics.Any(item=>item.Severity=="Error")){return new([],new(hash,"",diagnostics,[]));}
        var bytes=pe.ToArray();var artifactHash=Convert.ToHexString(SHA256.HashData(bytes));QueryAuditMetadata? audit=null;if(auditEntry is not null){audit=QueryAuditMetadataBuilder.Create(auditEntry,schemaCompilation,bytes,hash,artifactHash);}pe.Position=0;return new(bytes,new(hash,artifactHash,diagnostics,ExecutionPlanDocument.Read(pe))){AuditMetadata=audit};
    }
    internal static DiagnosticInfo[] ValidatePolicy(CSharpCompilation compilation,SyntaxTree[] trees){
        var errors=new List<DiagnosticInfo>();
        foreach(var tree in trees){var model=compilation.GetSemanticModel(tree);
            foreach(var node in tree.GetRoot().DescendantNodes()){
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
                if(node is InvocationExpressionSyntax or ObjectCreationExpressionSyntax or MemberAccessExpressionSyntax or AttributeSyntax){
                    var symbol=model.GetSymbolInfo(node).Symbol;var type=symbol?.ContainingType;var ns=type?.ContainingNamespace.ToDisplayString()??"";
                    if(type is not null&&!AllowedSystemType(type)){reason="DB処理・計算の対応範囲外のAPIです: "+symbol?.ToDisplayString();}
                    if(ns.StartsWith("Seamless.Management",StringComparison.Ordinal)&&type?.ToDisplayString()!="Seamless.Management.Server.ManagementExecutionContext"){reason="管理用APIは問い合わせコードから利用できません。";}
                    if(ns.StartsWith("Seamless.Execution",StringComparison.Ordinal)||type?.ToDisplayString()=="Seamless.Database.DatabaseContainerOptions"&&node is not IdentifierNameSyntax){reason="保存先・実行環境は管理サーバーが設定します。";}
                    if(ns.StartsWith("Seamless.Transactions",StringComparison.Ordinal)||ns.StartsWith("Seamless.Collections.Tsavorite",StringComparison.Ordinal)||symbol?.ContainingType?.ToDisplayString()=="Seamless.Database.DatabaseContainer"&&symbol.Name is "Dispose" or "BeginTransaction" or "BeginReadSession" or "BeginRelationImport" or "BeginUnpublishedSnapshot" or "Options"||symbol?.Name.Contains("Gateway",StringComparison.Ordinal)==true&&ns.StartsWith("Seamless.Database",StringComparison.Ordinal)){reason="取引・永続化・DBの寿命は管理サーバーが制御します。";}
                    if(node is ObjectCreationExpressionSyntax&&type is not null){for(var parent=type;parent is not null;parent=parent.BaseType){if(parent.ToDisplayString()=="Seamless.Database.DatabaseContainer"){reason="DBはManagementExecutionContextから取得してください。";break;}}}
                    if(ns.StartsWith("System.IO",StringComparison.Ordinal)||ns.StartsWith("System.Net",StringComparison.Ordinal)||ns.StartsWith("System.Reflection",StringComparison.Ordinal)||ns.StartsWith("System.Runtime.InteropServices",StringComparison.Ordinal)||ns.StartsWith("System.Runtime.Loader",StringComparison.Ordinal)||ns.StartsWith("System.Threading",StringComparison.Ordinal)&&type?.ToDisplayString()!="System.Threading.CancellationToken"||ns.StartsWith("Microsoft",StringComparison.Ordinal)
                        ||ns.StartsWith("System.Linq.Expressions",StringComparison.Ordinal)||type?.ToDisplayString() is "System.AppDomain" or "System.Environment" or "System.GC" or "System.Console" or "System.Diagnostics.Process" or "System.Type" or "System.Activator" or "System.Delegate"||symbol?.Name=="GetType"){reason="DB処理・計算から利用できないAPIです: "+symbol?.ToDisplayString();}
                }
                if(reason is not null){errors.Add(new("ManagementCodePolicy",reason,tree.FilePath,node.SpanStart,node.Span.Length));}
            }
        }
        return errors.ToArray();
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

/// <summary>検証時は検証DBだけを束縛する。任意名のDB取得は公開しない。</summary>
public sealed class ManagementExecutionContext(global::Seamless.Database.DatabaseContainer database,CancellationToken cancellationToken){
    public CancellationToken CancellationToken{get;}=cancellationToken;
    public T GetDatabase<T>() where T:global::Seamless.Database.DatabaseContainer{
        this.CancellationToken.ThrowIfCancellationRequested();return database as T??throw new ManagementException("SchemaMismatch","DB型が実行環境と一致しません。");
    }
}
