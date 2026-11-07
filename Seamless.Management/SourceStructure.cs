using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public sealed record SourceColumn(string Name,string Type);
public sealed record SourceObject(string Root,string Kind,string Schema,string Name,string Type,string File,int Start,int Length,SourceColumn[] Columns);
public sealed record SourceStructure(string[] Namespaces,string[] Types,SourceObject[] Objects){
    public static SourceStructure Read(SourceSnapshot source){
        SourceEditor.Validate(source);
        var options=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:source.Defines??[]);
        var trees=source.Files.OrderBy(file=>file.Path,StringComparer.Ordinal).Select(file=>CSharpSyntaxTree.ParseText(file.Text,options,file.Path)).ToArray();
        var compilation=CSharpCompilation.Create("SourceStructure",trees,options:new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var symbols=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach(var tree in trees){
            var model=compilation.GetSemanticModel(tree);
            foreach(var declaration in tree.GetRoot().DescendantNodes().Where(node=>node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)){
                if(model.GetDeclaredSymbol(declaration) is INamedTypeSymbol{Name.Length:>0} symbol){symbols.Add(symbol);}
            }
        }
        var roots=symbols.Where(symbol=>symbol.Name=="DatabaseGenerator"&&symbol.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface)
            .Select(symbol=>Namespace(symbol.ContainingNamespace)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var types=symbols.Where(IsTypeCandidate).Select(TypeName).Order(StringComparer.Ordinal).ToArray();
        var objects=new List<SourceObject>();
        foreach(var symbol in symbols.Where(symbol=>symbol.TypeKind==TypeKind.Struct&&symbol.ContainingType is null&&symbol.Arity==0)){
            var name=Namespace(symbol.ContainingNamespace);
            foreach(var root in roots){
                foreach(var kind in new[]{"Tables","Views","Work"}){
                    var prefix=kind;
                    if(root.Length>0){prefix=root+"."+kind;}
                    if(name!=prefix&&!name.StartsWith(prefix+".",StringComparison.Ordinal)){continue;}
                    var schema="";
                    if(name.Length>prefix.Length){schema=name[(prefix.Length+1)..];}
                    var declaration=symbol.DeclaringSyntaxReferences.OrderBy(reference=>reference.SyntaxTree.FilePath,StringComparer.Ordinal).ThenBy(reference=>reference.Span.Start).First();
                    var columns=symbol.GetMembers().OfType<IPropertySymbol>().Where(property=>!property.IsStatic&&!property.IsIndexer&&property.DeclaredAccessibility==Accessibility.Public)
                        .Select(property=>new SourceColumn(property.Name,ColumnType(property))).ToArray();
                    objects.Add(new(root,kind,schema,symbol.Name,TypeName(symbol),declaration.SyntaxTree.FilePath,declaration.Span.Start,declaration.Span.Length,columns));
                }
            }
        }
        return new(roots,types,objects.OrderBy(item=>item.Root,StringComparer.Ordinal).ThenBy(item=>item.Kind,StringComparer.Ordinal).ThenBy(item=>item.Schema,StringComparer.Ordinal).ThenBy(item=>item.Name,StringComparer.Ordinal).ToArray());
    }
    private static string Namespace(INamespaceSymbol symbol){if(symbol.IsGlobalNamespace){return "";}return symbol.ToDisplayString();}
    private static string TypeName(ITypeSymbol symbol)=>symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static string ColumnType(IPropertySymbol property){
        foreach(var reference in property.DeclaringSyntaxReferences){
            if(reference.GetSyntax() is PropertyDeclarationSyntax declaration){return declaration.Type.ToString();}
            if(reference.GetSyntax() is ParameterSyntax{Type:{} type}){return type.ToString();}
        }
        return TypeName(property.Type);
    }
    private static bool IsTypeCandidate(INamedTypeSymbol symbol){
        if(symbol.IsStatic){return false;}
        for(var current=symbol;current is not null;current=current.ContainingType){
            if(current.Arity>0||current.DeclaredAccessibility is not(Accessibility.Public or Accessibility.Internal)){return false;}
        }
        return true;
    }
}
