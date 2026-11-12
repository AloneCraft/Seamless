using Microsoft.CodeAnalysis;

namespace Seamless.Management;

public static partial class DatabaseDesigner{
    public static string[] Schemas(Definition definition){
        var container=Container(Compilation(definition),definition);
        var names=new HashSet<string>(StringComparer.Ordinal);
        var ancestors=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Walk(container,"");
        foreach(var item in SourceStructure.Read(definition.Source).Objects.Where(item=>item.Root==container.ContainingNamespace.ToDisplayString()&&item.Schema.Length>0)){
            names.Add(item.Schema);
        }
        return names.Order(StringComparer.Ordinal).ToArray();

        void Walk(INamedTypeSymbol owner,string prefix){
            if(!ancestors.Add(owner)){return;}
            try{
                foreach(var property in owner.GetMembers().OfType<IPropertySymbol>()){
                    if(property.Type is not INamedTypeSymbol type||!IsSchemaType(type)){continue;}
                    var path=prefix+property.Name;names.Add(path);Walk(type,path+".");
                }
            }finally{ancestors.Remove(owner);}
        }
    }
    public static Definition AddSchema(Definition definition,string name){
        Identifier(name);
        var owner=Container(Compilation(definition),definition);
        if(owner.GetMembers(name).Length>0||Schemas(definition).Contains(name,StringComparer.Ordinal)||Objects(definition).Any(item=>item.Schema.Length==0&&item.Name==name)){
            throw new ManagementException("Conflict","同じ名前のスキーマまたはオブジェクトがあります: "+name);
        }
        return EnsureSchema(definition,name);
    }
    private static bool IsSchemaType(INamedTypeSymbol type){
        for(var current=type;current is not null;current=current.BaseType){
            if(current.ToDisplayString()=="Seamless.Database.DatabaseSchema"){return true;}
        }
        return false;
    }
}
