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
    internal CSharpCompilation CompileSchemaForEditing(SourceSnapshot source){
        var prepared=this.Prepare(source,null,null);
        if(prepared.Policy.Length>0){throw new ManagementException("Validation",prepared.Policy[0].Message);}
        return (CSharpCompilation)RunSchemaGenerators(prepared.Compilation,prepared.Parse,out _);
    }
    private sealed record PreparedCompilation(SourceSnapshot Source,string Hash,CSharpParseOptions Parse,CSharpCompilation Compilation,SyntaxTree[] Trees,DiagnosticInfo[] Policy);
    private PreparedCompilation Prepare(SourceSnapshot source,IReadOnlyList<byte[]>? dependencies,string? generation){
        source=source.Freeze();SourceEditor.Validate(source);var hash=source.Hash();var name="Seamless.Managed_"+hash[..24];
        if(generation is not null){name+="_g"+generation;}
        var references=SourceEditor.PlatformReferences().ToList();
        var paths=references.OfType<PortableExecutableReference>().Select(reference=>reference.FilePath!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        void Add(Assembly assembly){if(assembly.IsDynamic){return;}var path=global::Seamless.Execution.CompilationReferences.PathFor(assembly);if(!paths.Add(path)){return;}references.Add(MetadataReference.CreateFromFile(path));foreach(var item in assembly.GetReferencedAssemblies()){Add(Assembly.Load(item));}}
        Add(typeof(global::Seamless.Database.DatabaseContainer).Assembly);Add(typeof(ManagementExecutionContext).Assembly);Add(typeof(global::Seamless.Linq.Enumerable).Assembly);
        // 取り込んだ DB(SQL Server から)は bit の型と、訳したビューが呼ぶ組み込み関数を使う。使わない DB の成果物には参照が残らない。
        Add(typeof(global::Seamless.Database.SqlServer.Types.Bit).Assembly);
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
    // 使用制限の規則は DB 生成器の解析器(SDK のビルドでも働く)と同じものを使う。
    internal static DiagnosticInfo[] ValidatePolicy(CSharpCompilation compilation,SyntaxTree[] trees)=>
        trees.SelectMany(tree=>global::Seamless.Database.Generator.ManagedDatabaseCodePolicyAnalyzer.Check(compilation.GetSemanticModel(tree),CancellationToken.None)
            .Select(item=>new DiagnosticInfo("ManagementCodePolicy",item.Reason,tree.FilePath,item.Node.SpanStart,item.Node.Span.Length))).ToArray();
}

