using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management.Server;

internal sealed record QueryAuditMetadata(int Format,string SourceHash,string ArtifactHash,string? Namespace,string? TypeName,string? MethodName,string? MethodSignature,Guid ModuleId,int MetadataToken,string? QueryTemplate);

internal static class QueryAuditMetadataBuilder{
    internal static QueryAuditMetadata Create(IMethodSymbol method,Compilation compilation,byte[] bytes,string sourceHash,string artifactHash){
        using var stream=new MemoryStream(bytes);using var pe=new PEReader(stream);var reader=pe.GetMetadataReader();var name=MetadataName(method.ContainingType);var matches=new List<MethodDefinitionHandle>();
        foreach(var handle in reader.TypeDefinitions){if(MetadataName(reader,handle)!=name){continue;}foreach(var candidate in reader.GetTypeDefinition(handle).GetMethods()){var entry=reader.GetMethodDefinition(candidate);if(reader.GetString(entry.Name)==method.MetadataName&&(entry.Attributes&MethodAttributes.MemberAccessMask)==MethodAttributes.Public&&(entry.Attributes&MethodAttributes.Static)!=0&&entry.GetGenericParameters().Count==0){matches.Add(candidate);}}}
        if(matches.Count!=1){throw new InvalidDataException("問い合わせの公開入口とDLLメタデータが一致しません。");}
        var ns=method.ContainingNamespace.ToDisplayString();if(method.ContainingNamespace.IsGlobalNamespace){ns="";}var type=method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);if(ns.Length>0&&type.StartsWith(ns+".",StringComparison.Ordinal)){type=type[(ns.Length+1)..];}
        var parameters=string.Join(",",method.Parameters.Select(static item=>item.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+" @"+item.Name));var signature=method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+" "+method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)+"."+method.Name+"("+parameters+")";
        return new(1,sourceHash,artifactHash,Bound(ns,1024),Bound(type,1024),Bound(method.Name,512),Bound(signature,2048),reader.GetGuid(reader.GetModuleDefinition().Mvid),MetadataTokens.GetToken(matches[0]),Template(method,compilation));
    }
    internal static void Validate(QueryAuditMetadata value,string sourceHash,string artifactHash){if(value.Format!=1||value.SourceHash!=sourceHash||value.ArtifactHash!=artifactHash||value.ModuleId==Guid.Empty||(value.MetadataToken&unchecked((int)0xff000000))!=0x06000000){throw new InvalidDataException("問い合わせの監査情報と成果物の識別情報が一致しません。");}}
    private static string? Bound(string value,int maximum){if(value.Length>maximum){return null;}return value;}
    private static string MetadataName(INamedTypeSymbol type){if(type.ContainingType is{} parent){return MetadataName(parent)+"+"+type.MetadataName;}if(type.ContainingNamespace.IsGlobalNamespace){return type.MetadataName;}return type.ContainingNamespace.ToDisplayString()+"."+type.MetadataName;}
    private static string MetadataName(MetadataReader reader,TypeDefinitionHandle handle){var type=reader.GetTypeDefinition(handle);var name=reader.GetString(type.Name);if(!type.GetDeclaringType().IsNil){return MetadataName(reader,type.GetDeclaringType())+"+"+name;}var ns=reader.GetString(type.Namespace);if(ns.Length==0){return name;}return ns+"."+name;}
    private static string? Template(IMethodSymbol method,Compilation compilation){
        method=method.PartialImplementationPart??method;var roots=new HashSet<SyntaxNode>();
        foreach(var reference in method.DeclaringSyntaxReferences){
            if(reference.GetSyntax() is not MethodDeclarationSyntax declaration){continue;}SyntaxNode? body=declaration.Body;if(body is null){body=declaration.ExpressionBody?.Expression;}if(body is null){continue;}var model=compilation.GetSemanticModel(body.SyntaxTree);
            foreach(var node in body.DescendantNodesAndSelf()){
                var query=node is QueryExpressionSyntax;
                if(node is InvocationExpressionSyntax call&&model.GetSymbolInfo(call).Symbol is IMethodSymbol invoked){var original=invoked.ReducedFrom??invoked;var ns=original.ContainingNamespace.ToDisplayString();query=ns is "System.Linq" or "Seamless.Linq";}
                if(!query){continue;}var root=node;foreach(var parent in node.Ancestors()){if(ReferenceEquals(parent,body)){break;}if(parent is InvocationExpressionSyntax or QueryExpressionSyntax){root=parent;}}roots.Add(root);
            }
        }
        if(roots.Count==0){return null;}var outer=roots.Where(node=>!node.Ancestors().Any(roots.Contains)).OrderBy(static node=>node.SpanStart).ToArray();var result=new StringBuilder();var rewriter=new ValueRedactor();var omitted=false;var count=0;
        foreach(var node in outer){if(count++==32){omitted=true;break;}string text;if(node.FullSpan.Length>65536){text="[大きな式を省略]";}else{text=rewriter.Visit(node)!.NormalizeWhitespace().ToFullString();}if(result.Length+text.Length+1>8160){omitted=true;break;}if(result.Length>0){result.AppendLine();}result.Append(text);}
        if(omitted){result.AppendLine().Append("[以降を省略]");}return result.ToString();
    }
    private sealed class ValueRedactor:CSharpSyntaxRewriter{
        private int next;
        private IdentifierNameSyntax Placeholder(){var name="@p"+this.next.ToString(System.Globalization.CultureInfo.InvariantCulture);this.next++;return SyntaxFactory.IdentifierName(name);}
        public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)=>this.Placeholder();
        public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)=>this.Placeholder();
        public override SyntaxToken VisitToken(SyntaxToken token)=>base.VisitToken(token).WithoutTrivia();
    }
}
