using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

/// <remarks>
/// ビュー・関数は、DB生成器が作る入口のインターフェース(コンテナの入れ子の IReader、スキーマは I&lt;スキーマ&gt;Reader)への拡張メンバーとして書く。
/// ビューは ISet&lt;行&gt; を返すプロパティ、テーブル関数は ISet&lt;行&gt; を返すメソッド、スカラー関数はそれ以外の値を返すメソッド。
/// 設計器のコンパイルは生成器を動かさないので、受け手の型は解決できない。書かれた「コンテナ型.インターフェース名」の形から入口を読む(DB生成器と同じ規則)。
/// </remarks>
public static partial class DatabaseDesigner{
    private sealed record QueryMember(ISymbol Member,string Schema,string Kind,ITypeSymbol Result,MemberDeclarationSyntax Syntax,string Receiver){
        public string Path=>string.Join(".",new[]{this.Schema,this.Member.Name}.Where(value=>value.Length>0));
    }

    private static string ReaderInterfaceName(string schema)=>schema.Length==0?"IReader":"I"+string.Concat(schema.Split('.'))+"Reader";

    private static QueryMember[] QueryMembers(CSharpCompilation compilation,INamedTypeSymbol container,IReadOnlyCollection<string> schemas){
        var interfaces=schemas.Prepend("").Distinct(StringComparer.Ordinal).ToDictionary(ReaderInterfaceName,schema=>schema,StringComparer.Ordinal);
        var members=new List<QueryMember>();
        foreach(var tree in compilation.SyntaxTrees){
            var model=compilation.GetSemanticModel(tree);
            foreach(var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()){
                if(model.GetDeclaredSymbol(declaration) is not{IsStatic:true} type||type.ContainingType is not null){continue;}
                foreach(var extension in type.GetTypeMembers().Where(static item=>item.IsExtension&&item.Arity==0)){
                    if(extension.ExtensionParameter?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not ParameterSyntax{Type:QualifiedNameSyntax receiver} parameter
                        ||receiver.SyntaxTree!=tree||!declaration.Span.Contains(parameter.Span)
                        ||!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(receiver.Left).Symbol,container)
                        ||!interfaces.TryGetValue(receiver.Right.Identifier.ValueText,out var schema)){continue;}
                    foreach(var member in extension.GetMembers().Where(static member=>!member.IsStatic&&member.DeclaredAccessibility==Accessibility.Public)){
                        if(member.DeclaringSyntaxReferences.Where(reference=>reference.SyntaxTree==tree).Select(reference=>reference.GetSyntax()).FirstOrDefault() is not MemberDeclarationSyntax syntax){continue;}
                        if(member is IPropertySymbol{IsIndexer:false,GetMethod:not null,SetMethod:null} property){
                            var element=SetElement(property.Type);
                            members.Add(new(member,schema,element is null?"Property":"View",element??property.Type,syntax,parameter.Identifier.ValueText));
                        }
                        else if(member is IMethodSymbol{MethodKind:MethodKind.Ordinary,IsGenericMethod:false,ReturnsVoid:false} method){
                            if(SetElement(method.ReturnType) is{} element){members.Add(new(member,schema,"TableFunction",element,syntax,parameter.Identifier.ValueText));}
                            else{members.Add(new(member,schema,"ScalarFunction",method.ReturnType,syntax,parameter.Identifier.ValueText));}
                        }
                    }
                }
            }
        }
        return members.ToArray();
    }
    private static ITypeSymbol? SetElement(ITypeSymbol type)=>type is INamedTypeSymbol{Name:"ISet",TypeArguments.Length:1} set&&set.ContainingNamespace.ToDisplayString()=="Seamless.Collections"?set.TypeArguments[0]:null;

    private static QueryMember? FindQueryMember(CSharpCompilation compilation,Definition definition,string path)=>
        QueryMembers(compilation,Container(compilation,definition),Schemas(definition)).SingleOrDefault(member=>member.Path==path);
}
