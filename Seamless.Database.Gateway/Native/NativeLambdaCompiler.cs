using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Native;

/// <summary>有限ノードを検証して型付きC#へ描画し、FLinqを通す準備処理。</summary>
public sealed class NativeLambdaCompiler<TDatabase>{
    private readonly NativeLambdaSchema<TDatabase> schema;
    public NativeLambdaCompiler(NativeLambdaSchema<TDatabase> schema){this.schema=schema??throw new ArgumentNullException(nameof(schema));this.schema.Seal();}
    internal NativeLambdaSchema<TDatabase> Schema=>this.schema;

    internal NativeCompiledLambda Compile(NativeLambdaProgram input){
        // ローカル呼出元の配列も共有せず、受信と同じ検証経路へ通す。
        var program=NativeLambdaProgram.Decode(input.Encode());
        if(program.DatabaseType!=typeof(TDatabase).FullName||program.DatabaseContract!=this.schema.ContractId){throw new NativeLambdaException("Contract","The lambda database surface does not match.",true);}
        var arguments=this.schema.Contract(program.ArgumentsType);var result=this.schema.Contract(program.ResultType);
        if(NativeWireContract.For(arguments)!=program.ArgumentsContract||NativeWireContract.For(result)!=program.ResultContract){throw new NativeLambdaException("Contract","The lambda wire contract does not match.",true);}
        NativeWireValidator argumentValidator,resultValidator;
        try{this.schema.WireLimits.CheckProfile();argumentValidator=NativeWireValidation.For(arguments);resultValidator=NativeWireValidation.For(result);}
        catch(NotSupportedException error){throw new NativeLambdaException("Contract",error.Message,true);}
        var body=new NativeLambdaRenderer(this.schema.Types).Render(program.Body);
        var parse=new CSharpParseOptions(LanguageVersion.CSharp14).WithFeatures([new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        var source="using System.Linq;[global::Seamless.FLinq.LinqIntercept(true)]public sealed class LambdaEntry{"+
            "private static "+NativeLambdaRenderer.CSharp(result)+" Run("+NativeLambdaRenderer.CSharp(typeof(TDatabase))+" p0,"+NativeLambdaRenderer.CSharp(arguments)+" p1,global::System.Threading.CancellationToken __cancellation)"+body+
            "public static byte[] Invoke(object database,byte[] arguments,global::System.Threading.CancellationToken cancellation){return global::MemoryPack.MemoryPackSerializer.Serialize(Run(("+NativeLambdaRenderer.CSharp(typeof(TDatabase))+")database,global::MemoryPack.MemoryPackSerializer.Deserialize<"+NativeLambdaRenderer.CSharp(arguments)+">(arguments)!,cancellation));}}";
        var tree=CSharpSyntaxTree.ParseText(source,parse,"Seamless.Lambda.g.cs",Encoding.UTF8);
        var paths=((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var assemblies=new Dictionary<string,Assembly>(StringComparer.OrdinalIgnoreCase);
        void Reference(Assembly assembly){
            if(assembly.IsDynamic||string.IsNullOrEmpty(assembly.Location)||!assemblies.TryAdd(assembly.GetName().Name!,assembly)){return;}
            paths.Add(assembly.Location);
            var owner=AssemblyLoadContext.GetLoadContext(assembly)!;
            foreach(var name in assembly.GetReferencedAssemblies()){Reference(owner.LoadFromAssemblyName(name));}
        }
        Reference(typeof(TDatabase).Assembly);Reference(arguments.Assembly);Reference(result.Assembly);Reference(typeof(NativeLambdaProgram).Assembly);Reference(typeof(Seamless.Linq.Enumerable).Assembly);
        var references=paths.Select(static path=>MetadataReference.CreateFromFile(path));
        var hash=Convert.ToHexString(SHA256.HashData(program.Encode()));
        var compilation=CSharpCompilation.Create("Seamless.Lambda_"+hash[..24],[tree],references,new(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release,allowUnsafe:true,nullableContextOptions:NullableContextOptions.Enable,deterministic:true));
        var access=this.Validate(compilation,tree);
        foreach(var demand in access){if(!this.schema.Authorize(demand)){throw new NativeLambdaException("Permission","The lambda requires an operation that is not permitted: "+demand.Table+"/"+demand.Operation,true);}}
        GeneratorDriver generator=CSharpGeneratorDriver.Create([new Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:parse);
        generator.RunGeneratorsAndUpdateCompilation(compilation,out var generated,out var diagnostics);CheckDiagnostics(diagnostics);
        using var pe=new MemoryStream();var emitted=generated.Emit(pe);CheckDiagnostics(emitted.Diagnostics);
        var context=new LambdaLoadContext(assemblies);
        try{
            pe.Position=0;var assembly=context.LoadFromStream(pe);var invoke=assembly.GetType("LambdaEntry",true)!.GetMethod("Invoke",BindingFlags.Public|BindingFlags.Static)!.CreateDelegate<Func<object,byte[],CancellationToken,byte[]>>();
            return new(context,invoke,access,program.ArgumentsContract,program.ResultContract,source,argumentValidator,resultValidator,this.schema.WireLimits);
        }catch{context.Unload();throw;}
    }

    private NativeLambdaAccess[] Validate(CSharpCompilation compilation,SyntaxTree tree){
        var model=compilation.GetSemanticModel(tree);var method=tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static item=>item.Identifier.ValueText=="Run");
        var databaseSymbol=model.GetTypeInfo(method.ParameterList.Parameters[0].Type!).Type;
        var tables=new HashSet<string>(StringComparer.Ordinal);
        var aliases=new Dictionary<ISymbol,HashSet<string>>(SymbolEqualityComparer.Default);
        var demands=new HashSet<NativeLambdaAccess>();
        bool IsTable(IPropertySymbol property)=>SymbolEqualityComparer.Default.Equals(property.ContainingType,databaseSymbol)&&this.schema.Tables.ContainsKey(property.Name);
        HashSet<string> Roots(SyntaxNode expression){
            var result=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in expression.DescendantNodesAndSelf()){
                var symbol=model.GetSymbolInfo(item).Symbol;
                if(symbol is IPropertySymbol property&&IsTable(property)){result.Add(property.Name);}
                if(symbol is not null&&aliases.TryGetValue(symbol,out var names)){result.UnionWith(names);}
            }
            return result;
        }
        void Demand(SyntaxNode expression,string operation){foreach(var table in Roots(expression)){demands.Add(new(table,operation));}}
        // ローカル変数を通したテーブル・クエリの別名も追跡する。再代入は和集合で保守的に扱う。
        var bodyNodes=method.Body!.DescendantNodes().ToArray();
        for(var pass=0;pass<=bodyNodes.Length;pass++){
            var changed=false;
            foreach(var item in bodyNodes){
                ISymbol? symbol=null;SyntaxNode? value=null;
                if(item is VariableDeclaratorSyntax{Initializer:{} initializer} variable){symbol=model.GetDeclaredSymbol(variable);value=initializer.Value;}
                else if(item is AssignmentExpressionSyntax assignment&&assignment.Left is IdentifierNameSyntax){symbol=model.GetSymbolInfo(assignment.Left).Symbol;value=assignment.Right;}
                else if(item is ForEachStatementSyntax loop){symbol=model.GetDeclaredSymbol(loop);value=loop.Expression;}
                if(symbol is null||value is null){continue;}
                if(!aliases.TryGetValue(symbol,out var names)){names=new(StringComparer.Ordinal);aliases.Add(symbol,names);}
                var before=names.Count;names.UnionWith(Roots(value));changed|=before!=names.Count;
            }
            if(!changed){break;}
        }
        var allowed=this.schema.Types.Values.Select(static type=>type.FullName!.Replace('+','.')).ToHashSet(StringComparer.Ordinal);
        foreach(var node in bodyNodes){
            if(node is AssignmentExpressionSyntax assignment&&assignment.Left is IdentifierNameSyntax{Identifier.ValueText:"p0"}){throw Invalid();}
            if(node is ForEachStatementSyntax loop){Demand(loop.Expression,"Read");}
            if(node is not(InvocationExpressionSyntax or MemberAccessExpressionSyntax or ElementAccessExpressionSyntax or ObjectCreationExpressionSyntax)){continue;}
            var symbol=model.GetSymbolInfo(node).Symbol;
            var owner=symbol?.ContainingType?.OriginalDefinition.ToDisplayString();
            if(symbol is IPropertySymbol property&&SymbolEqualityComparer.Default.Equals(property.ContainingType,databaseSymbol)){if(!IsTable(property)){throw Invalid();}tables.Add(property.Name);continue;}
            if(symbol is IMethodSymbol call){
                if(node is MemberAccessExpressionSyntax&&node.Parent is InvocationExpressionSyntax invocation&&invocation.Expression==node){continue;}
                if(owner=="System.Linq.Enumerable"){Demand(node,"Read");continue;}
                if(owner=="Seamless.Database.Client.NativeTable<TKey, TRecord>"){
                    if(node is not InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax receiver}){throw Invalid();}
                    if(call.Name=="Add"){Demand(receiver.Expression,"Insert");}
                    else if(call.Name=="Remove"){Demand(receiver.Expression,"Delete");}
                    else if(call.Name=="Replace"){Demand(receiver.Expression,"Update");}
                    else{throw Invalid();}
                    continue;
                }
                if(owner=="string"&&call.Name is "Contains" or "StartsWith" or "EndsWith" or "Substring" or "ToUpperInvariant" or "ToLowerInvariant"){continue;}
                if(owner=="System.Threading.CancellationToken"&&call.Name=="ThrowIfCancellationRequested"){continue;}
                if(call.MethodKind==MethodKind.Constructor&&(allowed.Contains(owner??"")||owner=="System.InvalidOperationException")){continue;}
                throw Invalid();
            }
            if(symbol is IPropertySymbol or IFieldSymbol){
                if(owner=="Seamless.Database.Client.NativeTable<TKey, TRecord>"){
                    var operation="Read";if(node.Parent is AssignmentExpressionSyntax assigned&&assigned.Left==node){operation="Update";}
                    Demand(node,operation);continue;
                }
                if(allowed.Contains(owner??"")||owner is "string" or "System.Array"||owner?.StartsWith("System.Nullable<",StringComparison.Ordinal)==true){continue;}
                throw Invalid();
            }
        }
        foreach(var table in tables){if(!demands.Any(demand=>demand.Table==table)){demands.Add(new(table,"Read"));}}
        return demands.OrderBy(static demand=>demand.Table,StringComparer.Ordinal).ThenBy(static demand=>demand.Operation,StringComparer.Ordinal).ToArray();
    }
    private static void CheckDiagnostics(IEnumerable<Diagnostic> diagnostics){
        var errors=diagnostics.Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error||diagnostic.Id is "FLINQ002" or "CS8785").ToArray();
        if(errors.Length>0){throw new NativeLambdaException("Compilation",string.Join("\n",errors.Take(20).Select(static diagnostic=>diagnostic.ToString())),true);}
    }
    private static NativeLambdaException Invalid()=>new("Structure","The lambda refers to a member or operation outside the public contract.",true);
    private sealed class LambdaLoadContext(IReadOnlyDictionary<string,Assembly> assemblies):AssemblyLoadContext(isCollectible:true){
        protected override Assembly? Load(AssemblyName name){
            if(assemblies.TryGetValue(name.Name!,out var assembly)&&AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name)){return assembly;}
            return null;
        }
    }
}

internal sealed class NativeCompiledLambda(AssemblyLoadContext context,Func<object,byte[],CancellationToken,byte[]> invoke,NativeLambdaAccess[] access,string argumentsContract,string resultContract,string source,NativeWireValidator argumentValidator,NativeWireValidator resultValidator,NativeWireLimits limits):IDisposable{
    private AssemblyLoadContext? loadContext=context;
    private Func<object,byte[],CancellationToken,byte[]>? execute=invoke;
    public NativeLambdaAccess[] Access{get;}=access;
    public string ArgumentsContract{get;}=argumentsContract;
    public string ResultContract{get;}=resultContract;
    public string Source{get;}=source;
    public bool IsReadOnly=>this.Access.All(static item=>item.Operation=="Read");
    public void ValidateArguments(ReadOnlySpan<byte> arguments){try{argumentValidator.Validate(arguments,limits);}catch(InvalidDataException error){throw new NativeLambdaException("Arguments",error.Message,true);}}
    public byte[] Invoke(object database,byte[] arguments,CancellationToken cancellation){var action=Volatile.Read(ref this.execute)??throw new ObjectDisposedException(nameof(NativeCompiledLambda));var result=action(database,arguments,cancellation);resultValidator.Validate(result,limits);return result;}
    public void Dispose(){Interlocked.Exchange(ref this.execute,null);Interlocked.Exchange(ref this.loadContext,null)?.Unload();}
}
