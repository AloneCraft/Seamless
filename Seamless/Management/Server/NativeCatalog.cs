using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Database;

namespace Seamless.Management.Server;

/// <summary>型の形だけを調べる。コンストラクター、getter、関数、属性のコードを呼び出さない。</summary>
internal static class NativeCatalog{
    internal sealed record Item(CatalogObjectDetails Details);
    internal static IEnumerable<string> Schemas(Type type)=>SchemaPaths(type,"",new HashSet<Type>());
    private static IEnumerable<string> SchemaPaths(Type type,string prefix,HashSet<Type> ancestors){
        if(!ancestors.Add(type)){yield break;}try{
            foreach(var property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.GetIndexParameters().Length==0&&typeof(DatabaseSchema).IsAssignableFrom(property.PropertyType))){
                var path=prefix+property.Name;yield return path;foreach(var child in SchemaPaths(property.PropertyType,path+".",ancestors)){yield return child;}
            }
        }finally{ancestors.Remove(type);}
    }
    internal static IEnumerable<Item> Read(Type type,DefinitionVersion? version){
        CSharpCompilation? source=null;
        if(version is not null){source=CSharpCompilation.Create("CatalogSource",version.Definition.Source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,path:file.Path)),SourceEditor.PlatformReferences());}
        return Walk(type,"","",new HashSet<Type>(),source?.GetTypeByMetadataName(type.FullName!),version).Concat(Queries(type,source,version));
    }
    // ビュー・関数は、生成される入口(コンテナの入れ子の IReader、スキーマは I<スキーマ>Reader)への拡張メンバー。
    // 拡張メンバーは、受け手を先頭の引数にとる静的メソッド(プロパティは get_名前)として静的クラスへコンパイルされるので、その形から読む(呼び出さない)。
    // 種類の決め方は DB生成器(Shared/Database.Generation の CollectQueries)と同じ。
    private static IEnumerable<Item> Queries(Type container,CSharpCompilation? source,DefinitionVersion? version){
        var prefixes=SchemaPaths(container,"",new HashSet<Type>()).Prepend("").ToArray();
        var interfaces=new Dictionary<Type,string>();
        foreach(var prefix in prefixes){
            var name=prefix.Length==0?"IReader":"I"+string.Concat(prefix.Split('.'))+"Reader";
            if(container.GetNestedType(name,BindingFlags.Public) is{IsInterface:true} reader){interfaces[reader]=prefix.Length==0?"":prefix+".";}
        }
        if(interfaces.Count==0){yield break;}
        var extensions=source?.SyntaxTrees.SelectMany(tree=>tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Select(declaration=>source.GetSemanticModel(tree).GetDeclaredSymbol(declaration)))
            .OfType<INamedTypeSymbol>().SelectMany(type=>type.GetTypeMembers().Where(member=>member.IsExtension)).ToArray()??[];
        foreach(var type in container.Assembly.GetTypes().Where(type=>type.IsAbstract&&type.IsSealed&&!type.IsNested).OrderBy(type=>type.FullName,StringComparer.Ordinal)){
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(method=>method.Name,StringComparer.Ordinal)){
                var parameters=method.GetParameters();
                if(method.ContainsGenericParameters||method.ReturnType==typeof(void)||parameters.Length==0||!interfaces.TryGetValue(parameters[0].ParameterType,out var prefix)){continue;}
                // 拡張プロパティの getter の実装は get_名前 という静的メソッドになる(特別な名前の印は付かない)。
                // 同じ静的クラスの拡張ブロックの印の型(入れ子の型)にその名前のプロパティがあるかで、プロパティの実装だと判断する。
                var property=method.Name.StartsWith("get_",StringComparison.Ordinal)&&parameters.Length==1
                    &&type.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic).Any(nested=>nested.GetProperty(method.Name["get_".Length..],BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly) is not null);
                var memberName=property?method.Name["get_".Length..]:method.Name;
                var set=method.ReturnType.IsGenericType&&method.ReturnType.GetGenericTypeDefinition()==typeof(global::Seamless.Collections.ISet<>);
                string kind;
                if(property){kind=set?"View":"Property";}
                else if(method.IsSpecialName||method.Name.StartsWith("get_",StringComparison.Ordinal)||method.Name.StartsWith("set_",StringComparison.Ordinal)){continue;}
                else{kind=set?"TableFunction":"ScalarFunction";}
                var record=set?method.ReturnType.GetGenericArguments()[0]:method.ReturnType;
                var schema=prefix.TrimEnd('.');var id=prefix+memberName;
                var memberSymbol=extensions.Where(extension=>extension.ContainingType.ToDisplayString()==type.FullName?.Replace('+','.'))
                    .SelectMany(extension=>extension.GetMembers(memberName)).FirstOrDefault(member=>member is IPropertySymbol==property);
                var location=memberSymbol?.Locations.FirstOrDefault(item=>item.IsInSource);
                var catalogSource=new CatalogSource(version?.Definition.Id??"",version?.Version??0,version?.ArtifactHash??"",location?.SourceTree?.FilePath,location?.SourceSpan.Start??0,location?.SourceSpan.Length??0,location is not null);
                var descriptor=new CatalogObject(id,memberName,schema,kind,Display(record),null);
                yield return new(new(descriptor,kind=="ScalarFunction"?[]:Fields(record),[],parameters.Skip(1).Select(parameter=>new CatalogField(parameter.Name??"",Display(parameter.ParameterType))).ToArray(),kind=="View"?null:Display(record),catalogSource,[]));
            }
        }
    }
    private static IEnumerable<Item> Walk(Type type,string path,string schema,HashSet<Type> ancestors,INamedTypeSymbol? symbol,DefinitionVersion? version){
        if(!ancestors.Add(type)){yield break;}
        try{
            foreach(var member in type.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.NonPublic).OrderBy(member=>member.Name,StringComparer.Ordinal)){
                Type? value=null;ParameterInfo[] parameters=[];
                if(member is PropertyInfo property&&property.GetIndexParameters().Length==0&&property.GetMethod is{} getter&&(getter.IsPublic||typeof(ManagementDatabase).IsAssignableFrom(type))){value=property.PropertyType;}
                else if(member is MethodInfo{IsPublic:true,IsSpecialName:false,ContainsGenericParameters:false} method){value=method.ReturnType;parameters=method.GetParameters();}
                if(value is null){continue;}
                var id=path+member.Name;var memberSymbol=symbol?.GetMembers(member.Name).FirstOrDefault();
                if(typeof(DatabaseSchema).IsAssignableFrom(value)){
                    foreach(var nested in Walk(value,id+".",id,ancestors,(memberSymbol as IPropertySymbol)?.Type as INamedTypeSymbol,version)){yield return nested;}continue;
                }
                if(!value.IsGenericType){continue;}var generic=value.GetGenericTypeDefinition();string? kind=null;
                if(generic==typeof(Table<,>)||generic==typeof(Table<>)){kind="Table";if(typeof(ManagementDatabase).IsAssignableFrom(type)){kind="SystemTable";}}
                else if(generic==typeof(MaterializedView<,>)){kind="View";}
                else if(generic==typeof(StoredProcedure<>)){kind="StoredProcedure";}
                else if(generic.FullName=="Seamless.Database.System.SystemTable`2"){kind="SystemTable";}
                // 取引を通さない表(db.X)。行の一覧と編集はできるが、衝突は値の比較だけで見つける。
                else if(generic==typeof(Set<,>)||generic==typeof(Set<>)){kind="Set";}
                if(kind is null){continue;}
                var imported=member.GetCustomAttributesData().FirstOrDefault(attribute=>attribute.AttributeType.FullName==typeof(ImportedDatabaseObjectAttribute).FullName);
                var actualSchema=imported?.ConstructorArguments[0].Value as string??schema;var name=imported?.ConstructorArguments[1].Value as string??member.Name;
                var arguments=value.GetGenericArguments();var record=arguments[^1];var key=arguments.Length==2?arguments[0]:null;
                var location=memberSymbol?.Locations.FirstOrDefault(item=>item.IsInSource);
                var source=new CatalogSource(version?.Definition.Id??"",version?.Version??0,version?.ArtifactHash??"",location?.SourceTree?.FilePath,location?.SourceSpan.Start??0,location?.SourceSpan.Length??0,location is not null);
                var descriptor=new CatalogObject(id,name,actualSchema,kind,Display(record),key is null?null:Display(key));
                var relations=member.GetCustomAttributesData().Where(attribute=>attribute.AttributeType.FullName==typeof(ForeignKeyAttribute).FullName).Select(attribute=>{var args=attribute.ConstructorArguments;return new ForeignKeyDesign((string)args[0].Value!,(string)args[1].Value!,((IEnumerable<CustomAttributeTypedArgument>)args[2].Value!).Select(item=>(string)item.Value!).ToArray(),((IEnumerable<CustomAttributeTypedArgument>)args[3].Value!).Select(item=>(string)item.Value!).ToArray());}).ToArray();
                yield return new(new(descriptor,kind is "ScalarFunction" or "StoredProcedure"?[]:Fields(record),key is null?[]:Fields(key),parameters.Select(parameter=>new CatalogField(parameter.Name??"",Display(parameter.ParameterType))).ToArray(),kind is "TableFunction" or "ScalarFunction" or "StoredProcedure"?Display(record):null,source,relations));
            }
        }finally{ancestors.Remove(type);}
    }
    // Tables と [Key] で書いた行の生成された Key(IRecord<キーの型> の Key)は列ではない(キーの型の列は行の同じ名前の列として並ぶ)。
    // 手で書いた IRecord<TKey> の行(取引を通さない表の行など)の Key は本物の列なので残す(判定は値の JSON 化と同じ)。
    private static bool IsRecordKey(Type type,PropertyInfo property)=>property.Name=="Key"&&GeneratedRecordShape.IsDeclaredRecord(type);
    private static CatalogField[] Fields(Type type)=>type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.GetIndexParameters().Length==0&&!IsRecordKey(type,property)).Select(property=>new CatalogField(property.Name,Display(property.PropertyType))).Concat(type.GetFields(BindingFlags.Public|BindingFlags.Instance).Select(field=>new CatalogField(field.Name,Display(field.FieldType)))).ToArray();
    private static string Display(Type type)=>type.FullName??type.ToString();
}
