using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

/// <summary>ソースが正本。GUIの編集は版を照合した文字列範囲の置換だけに限定する。</summary>
public sealed class SourceEditor{
    private readonly MetadataReference[] references;
    public SourceEditor(IEnumerable<MetadataReference>? references=null){this.references=references?.ToArray()??PlatformReferences();}
    public static MetadataReference[] PlatformReferences()=>((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")??throw new InvalidOperationException("実行環境の参照一覧がありません。"))
        .Split(Path.PathSeparator).Distinct(StringComparer.OrdinalIgnoreCase).Select(path=>MetadataReference.CreateFromFile(path)).ToArray();
    public AnalysisResult Analyze(SourceSnapshot source){
        Validate(source);var options=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:source.Defines??[]);
        var trees=source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,options,file.Path)).ToArray();
        var compilation=CSharpCompilation.Create("ManagementEditor",trees,this.references,new(OutputKind.DynamicallyLinkedLibrary,nullableContextOptions:NullableContextOptions.Enable));
        var diagnostics=compilation.GetDiagnostics().Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(ToDiagnostic).ToArray();
        var nodes=new List<EditorNode>();
        foreach(var tree in trees){
            var model=compilation.GetSemanticModel(tree);var root=tree.GetRoot();
            foreach(var node in root.DescendantNodes()){
                string? kind=null,label=null;var editable=true;
                if(node is TypeDeclarationSyntax type){kind="Type";label=type.Identifier.ValueText;}
                else if(node is PropertyDeclarationSyntax property){kind="Property";label=property.Identifier.ValueText;}
                else if(node is FieldDeclarationSyntax field){kind="Field";label=string.Join(",",field.Declaration.Variables.Select(variable=>variable.Identifier.ValueText));}
                else if(node is MethodDeclarationSyntax method){kind="CustomCode";label=method.Identifier.ValueText;}
                else if(node is InvocationExpressionSyntax invocation){
                    var symbol=model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if(symbol is not null&&(symbol.ReducedFrom??symbol).ContainingType.ToDisplayString()=="System.Linq.Enumerable"){kind="Operator";label=symbol.Name;editable=symbol.ReducedFrom is not null;}
                }
                if(kind is null){continue;}
                var parent=node.Ancestors().FirstOrDefault(ancestor=>ancestor is TypeDeclarationSyntax or MethodDeclarationSyntax or InvocationExpressionSyntax);
                string? parentId=null;if(parent is not null){parentId=Id(tree.FilePath,parent.SpanStart,parent.Span.Length);}
                nodes.Add(new(Id(tree.FilePath,node.SpanStart,node.Span.Length),kind,label!,tree.FilePath,node.SpanStart,node.Span.Length,parentId,editable));
            }
        }
        var ids=nodes.Select(node=>node.Id).ToHashSet();
        return new(source.Hash(),nodes.Select(node=>{if(node.ParentId is not null&&!ids.Contains(node.ParentId)){return node with{ParentId=null};}return node;}).ToArray(),diagnostics,diagnostics.Length==0);
    }
    public SourceSnapshot Apply(SourceSnapshot source,SourceEdit edit){
        Validate(source);if(source.Hash()!=edit.SourceHash){throw new ManagementException("Conflict","編集中のソースが更新されています。");}
        var file=source.Files.SingleOrDefault(file=>file.Path==edit.Path)??throw new ManagementException("InvalidRequest","対象ソースがありません。");
        if(edit.Start<0||edit.Length<0||edit.Start>file.Text.Length-edit.Length){throw new ManagementException("InvalidRequest","編集範囲が不正です。");}
        if(edit.Length==0&&edit.Replacement.Length==0){return source;}
        var updated=file.Text.Remove(edit.Start,edit.Length).Insert(edit.Start,edit.Replacement);
        return source with{Files=source.Files.Select(item=>{if(ReferenceEquals(item,file)){return file with{Text=updated};}return item;}).ToArray()};
    }
    public static DiagnosticInfo ToDiagnostic(Diagnostic diagnostic){var span=diagnostic.Location.SourceSpan;return new(diagnostic.Id,diagnostic.GetMessage(),diagnostic.Location.SourceTree?.FilePath,span.Start,span.Length,diagnostic.Severity.ToString());}
    public static void Validate(SourceSnapshot source){
        if(source.TargetFramework!="net10.0"||source.LanguageVersion!="14"){throw new ManagementException("Unsupported","net10.0 / C# 14 のスナップショットを指定してください。");}
        if(source.Files.Length is 0 or >256||source.Files.Sum(file=>(long)file.Text.Length)>4*1024*1024||source.Files.Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=source.Files.Length){throw new ManagementException("InvalidRequest","ソースの件数・容量・パスが不正です。");}
        foreach(var file in source.Files){if(string.IsNullOrWhiteSpace(file.Path)||Path.IsPathRooted(file.Path)||file.Path.IndexOfAny([':','\0','*','?','"','<','>','|'])>=0||file.Path.Split('/','\\').Any(part=>part is ".." or "." or ""||part.EndsWith(' ')||part.EndsWith('.'))||!file.Path.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)){throw new ManagementException("InvalidRequest","相対的なC#ソースパスが必要です。");}}
    }
    private static string Id(string path,int start,int length)=>path+"#"+start+":"+length;
}
