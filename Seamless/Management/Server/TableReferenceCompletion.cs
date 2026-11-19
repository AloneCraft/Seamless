using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management.Server;

internal static class TableReferenceCompletion{
    internal static CodeCompletionResult Find(Definition definition,SourceSnapshot source,string path,int start,int end,string schema,CancellationToken cancellation){
        cancellation.ThrowIfCancellationRequested();
        var compilation=new RuntimeCompiler().CompileSchemaForEditing(source);
        var tree=compilation.SyntaxTrees.Single(item=>item.FilePath==path);
        var root=tree.GetRoot(cancellation);
        var hash=source.Hash();
        var token=root.FindToken(start);
        var extension=token.Parent?.AncestorsAndSelf().OfType<ExtensionBlockDeclarationSyntax>().FirstOrDefault();
        var parameter=extension?.ParameterList?.Parameters.SingleOrDefault();
        if(parameter?.Type is not{} type||token.Parent?.AncestorsAndSelf().FirstOrDefault(node=>node is BlockSyntax or ArrowExpressionClauseSyntax) is not{} body
            ||end>body.Span.End){return Unavailable();}
        var model=compilation.GetSemanticModel(tree);
        var reader=model.GetTypeInfo(type,cancellation).Type as INamedTypeSymbol;
        var db=compilation.GetTypeByMetadataName(definition.EntryType);
        var receiverSymbol=model.GetDeclaredSymbol(parameter,cancellation);
        if(reader is null||db is null||!SymbolEqualityComparer.Default.Equals(reader.ContainingType,db)
            ||model.LookupSymbols(start,name:parameter.Identifier.ValueText).Any(symbol=>symbol is ILocalSymbol or IParameterSymbol&&!SymbolEqualityComparer.Default.Equals(symbol,receiverSymbol))){return Unavailable();}
        var set=compilation.GetTypeByMetadataName("Seamless.Collections.ISet`1");
        var items=new List<CodeCompletionItem>();
        foreach(var table in DatabaseDesigner.Objects(definition).Where(item=>item.Kind is "Table" or "Set")){
            cancellation.ThrowIfCancellationRequested();
            var local=table.Id;
            var reachable=schema.Length==0||local.StartsWith(schema+".",StringComparison.Ordinal);
            if(reachable&&schema.Length>0){local=local[(schema.Length+1)..];}
            var parts=(reachable?local:"Database."+table.Id).Split('.');
            INamedTypeSymbol? current=reader;
            foreach(var part in parts){
                current=current is null?null:model.LookupSymbols(start,current,part).OfType<IPropertySymbol>()
                    .SingleOrDefault(property=>!property.IsStatic&&property.GetMethod is not null)?.Type as INamedTypeSymbol;
            }
            if(set is not null&&SymbolEqualityComparer.Default.Equals(current?.OriginalDefinition,set)){
                items.Add(new(table.Id,Identifier(parameter.Identifier.ValueText)+"."+string.Join(".",parts.Select(Identifier)),"Table"));
            }
        }
        if(start==end&&start>0&&root.FindToken(start-1) is var dot&&dot.IsKind(SyntaxKind.DotToken)&&dot.Span.End==start
            &&dot.Parent is MemberAccessExpressionSyntax{Expression:IdentifierNameSyntax receiver,Name.IsMissing:true}&&receiver.Identifier.ValueText==parameter.Identifier.ValueText){
            start=receiver.SpanStart;
        }
        return new(hash,path,start,end-start,items.ToArray(),UnavailableReason:items.Count==0?"この読み取り口から参照できるテーブルがありません。":null);

        CodeCompletionResult Unavailable()=>new(hash,path,start,end-start,[],UnavailableReason:"この位置のDBの読み取り口を解決できません。");
        static string Identifier(string name)=>SyntaxFacts.GetKeywordKind(name)!=SyntaxKind.None||SyntaxFacts.GetContextualKeywordKind(name)!=SyntaxKind.None?"@"+name:name;
    }
}
