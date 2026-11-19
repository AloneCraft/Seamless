using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public sealed record CodeCompletionItem(string DisplayText,string InsertText,string Kind);
public sealed record CodeCompletionResult(string SourceHash,string Path,int Start,int Length,CodeCompletionItem[] Items,bool Limited=false,string? UnavailableReason=null);

/// <summary>許可済みメタデータと入力ソースだけを用いる意味解析補完。実行や成果物の生成は行わない。</summary>
public sealed class CSharpCodeCompletion{
    public const int MaximumItems=100;
    private readonly MetadataReference[] references;

    public CSharpCodeCompletion(IEnumerable<MetadataReference>? references=null){
        this.references=references?.ToArray()??SourceEditor.PlatformReferences();
    }

    public CodeCompletionResult Complete(SourceSnapshot source,string path,int position,CancellationToken cancellationToken=default)=>
        this.Complete(source,path,position,null,cancellationToken);

    internal CodeCompletionResult Complete(SourceSnapshot source,string path,int position,Func<SourceSnapshot,CSharpCompilation>? compileSchema,CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        source=source.Freeze();
        SourceEditor.Validate(source);
        var file=source.Files.SingleOrDefault(file=>file.Path==path)??throw new ManagementException("InvalidRequest","対象ソースがありません。");
        if(position<0||position>file.Text.Length){throw new ManagementException("InvalidRequest","補完位置が不正です。");}
        var start=position;
        while(start>0&&SyntaxFacts.IsIdentifierPartCharacter(file.Text[start-1])){start--;}
        if(start>0&&file.Text[start-1]=='@'){start--;}
        var end=position;
        while(end<file.Text.Length&&SyntaxFacts.IsIdentifierPartCharacter(file.Text[end])){end++;}
        var hash=source.Hash();
        var prefix=file.Text[start..position].TrimStart('@');
        var options=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:source.Defines??[]);
        var original=CSharpSyntaxTree.ParseText(file.Text,options,path,cancellationToken:cancellationToken);
        var root=original.GetRoot(cancellationToken);
        if(IsNonCode(root,position)){
            return new(hash,path,start,end-start,[],UnavailableReason:"コメントまたは文字列の中です。");
        }
        // 未入力の識別子を構文上の名前として束縛し、末尾のドットでも同じ解析経路を使う。
        const string placeholder="__seamless_completion__";
        var text=file.Text.Remove(start,end-start).Insert(start,placeholder);
        SyntaxTree tree;
        CSharpCompilation compilation;
        if(compileSchema is null){
            tree=CSharpSyntaxTree.ParseText(text,options,path,cancellationToken:cancellationToken);
            var trees=source.Files.Select(item=>item.Path==path?tree:CSharpSyntaxTree.ParseText(item.Text,options,item.Path,cancellationToken:cancellationToken));
            compilation=CSharpCompilation.Create("ManagementCompletion",trees,this.references,new(OutputKind.DynamicallyLinkedLibrary,nullableContextOptions:NullableContextOptions.Enable));
        }else{
            var edited=source with{Files=source.Files.Select(item=>item.Path==path?item with{Text=text}:item).ToArray()};
            compilation=compileSchema(edited);
            tree=compilation.SyntaxTrees.Single(item=>item.FilePath==path);
        }
        var model=compilation.GetSemanticModel(tree);
        var name=tree.GetRoot(cancellationToken).FindToken(start).Parent;
        INamespaceOrTypeSymbol? container=null;
        bool? staticOnly=null;
        SyntaxNode? receiver=null;
        if(name?.Parent is MemberAccessExpressionSyntax access&&access.Name.SpanStart==start){receiver=access.Expression;}
        else if(name?.Parent is MemberBindingExpressionSyntax binding&&binding.Name.SpanStart==start){receiver=binding.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression;}
        else if(name?.Parent is QualifiedNameSyntax qualified&&qualified.Right.SpanStart==start){receiver=qualified.Left;}
        else if(name?.Parent is AliasQualifiedNameSyntax alias&&alias.Name.SpanStart==start){
            var symbol=model.GetAliasInfo(alias.Alias,cancellationToken);
            if(alias.Alias.Identifier.ValueText=="global"){container=compilation.GlobalNamespace;}
            else{container=symbol?.Target as INamespaceOrTypeSymbol;}
            staticOnly=true;
        }
        if(receiver is not null){
            var symbol=model.GetSymbolInfo(receiver,cancellationToken).Symbol;
            if(symbol is IAliasSymbol aliasSymbol){symbol=aliasSymbol.Target;}
            if(symbol is INamespaceOrTypeSymbol namespaceOrType){container=namespaceOrType;staticOnly=true;}
            else{container=model.GetTypeInfo(receiver,cancellationToken).Type;staticOnly=false;}
        }
        if((receiver is not null||staticOnly==true)&&(container is null||container is ITypeSymbol{TypeKind:TypeKind.Error})){
            return new(hash,path,start,end-start,[],UnavailableReason:"対象の型または名前空間を解決できません。");
        }
        var symbols=model.LookupSymbols(start,container,includeReducedExtensionMethods:true);
        var items=new Dictionary<(string,string),CodeCompletionItem>();
        foreach(var symbol in symbols){
            cancellationToken.ThrowIfCancellationRequested();
            if(!CanSuggest(symbol,staticOnly)||!symbol.Name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){continue;}
            var insertion=symbol.Name;
            if(SyntaxFacts.GetKeywordKind(insertion)!=SyntaxKind.None||SyntaxFacts.GetContextualKeywordKind(insertion)!=SyntaxKind.None){insertion="@"+insertion;}
            var item=new CodeCompletionItem(symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),insertion,symbol.Kind.ToString());
            items.TryAdd((symbol.Name,item.Kind),item);
        }
        var ordered=items.Values.OrderBy(item=>item.InsertText,StringComparer.Ordinal).ThenBy(item=>item.Kind,StringComparer.Ordinal).Take(MaximumItems+1).ToArray();
        return new(hash,path,start,end-start,ordered.Take(MaximumItems).ToArray(),ordered.Length>MaximumItems);
    }

    public SourceSnapshot Apply(SourceSnapshot latest,CodeCompletionResult result,int itemIndex){
        if(itemIndex<0||itemIndex>=result.Items.Length){throw new ManagementException("InvalidRequest","補完候補が不正です。");}
        return new SourceEditor(this.references).Apply(latest,new(result.SourceHash,result.Path,result.Start,result.Length,result.Items[itemIndex].InsertText));
    }

    private static bool CanSuggest(ISymbol symbol,bool? staticOnly){
        if(symbol.IsImplicitlyDeclared||symbol.Name.Length==0||!SyntaxFacts.IsValidIdentifier(symbol.Name)&&SyntaxFacts.GetKeywordKind(symbol.Name)==SyntaxKind.None){return false;}
        if(symbol is IMethodSymbol method&&method.MethodKind is not (MethodKind.Ordinary or MethodKind.ReducedExtension)){return false;}
        if(symbol is not (INamespaceSymbol or INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol or ILocalSymbol or IParameterSymbol or ITypeParameterSymbol)){return false;}
        if(staticOnly==true&&symbol is not INamespaceOrTypeSymbol&&!symbol.IsStatic){return false;}
        if(staticOnly==false&&(symbol is INamespaceOrTypeSymbol||symbol.IsStatic)){return false;}
        return true;
    }

    private static bool IsNonCode(SyntaxNode root,int position){
        if(root.FullSpan.Length==0){return false;}
        var at=Math.Min(position,root.FullSpan.End-1);
        if(position>0){
            var previous=root.FindTrivia(position-1,findInsideTrivia:true);
            if(previous.IsKind(SyntaxKind.SingleLineCommentTrivia)&&previous.Span.End==position){return true;}
        }
        var trivia=root.FindTrivia(at,findInsideTrivia:true);
        if(trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)||trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)||trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)||trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)||trivia.IsKind(SyntaxKind.DisabledTextTrivia)){return true;}
        var token=root.FindToken(at,findInsideTrivia:true);
        return token.Parent?.AncestorsAndSelf().Any(node=>node is LiteralExpressionSyntax literal&&literal.IsKind(SyntaxKind.StringLiteralExpression)||node is InterpolatedStringTextSyntax)==true;
    }
}
