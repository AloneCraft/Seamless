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
    internal static IEnumerable<Item> Read(Type type,DefinitionVersion? version,CatalogIdentities? identities=null){
        CSharpCompilation? source=null;
        if(version is not null){source=CSharpCompilation.Create("CatalogSource",version.Definition.Source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,path:file.Path)),SourceEditor.PlatformReferences());}
        identities??=version is null?null:DatabaseDesigner.ReadStableIdentities(version.Definition);
        return Walk(type,"","",new HashSet<Type>(),source?.GetTypeByMetadataName(type.FullName!),version).Select(item=>{
            if(identities is null){return item;}
            var details=item.Details;var id=details.Object.Id;
            return item with{Details=details with{Object=details.Object with{StableId=identities.Object(id),StableIdUnavailableReason=identities.UnavailableReason("Object",id)},Columns=details.Columns.Select(column=>column with{StableId=identities.Column(id,column.Name),StableIdUnavailableReason=identities.UnavailableReason("Column",id,column.Name)}).ToArray(),KeyMembers=details.KeyMembers.Select(column=>column with{StableId=identities.Column(id,column.Name),StableIdUnavailableReason=identities.UnavailableReason("Column",id,column.Name)}).ToArray()}};
        });
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
                else if(generic==typeof(MaterializedView<,>)||generic==typeof(global::Seamless.Collections.ISet<>)){kind="View";}
                else if(generic==typeof(TableFunction<>)){kind="TableFunction";}
                else if(generic==typeof(ScalarFunction<>)){kind="ScalarFunction";}
                else if(generic==typeof(StoredProcedure<>)){kind="StoredProcedure";}
                else if(generic.FullName=="Seamless.Database.System.SystemTable`2"){kind="SystemTable";}
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
    private static CatalogField[] Fields(Type type)=>type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.GetIndexParameters().Length==0).Select(property=>new CatalogField(property.Name,Display(property.PropertyType))).Concat(type.GetFields(BindingFlags.Public|BindingFlags.Instance).Select(field=>new CatalogField(field.Name,Display(field.FieldType)))).ToArray();
    private static string Display(Type type)=>type.FullName??type.ToString();
}
