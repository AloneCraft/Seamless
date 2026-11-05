using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.Management.Server;

internal sealed class MigrationLoadContext(Assembly before,Assembly after):AssemblyLoadContext(isCollectible:true){
    protected override Assembly? Load(AssemblyName name){foreach(var assembly in new[]{before,after}){if(AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name)){return assembly;}}return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly=>AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name));}
}
/// <summary>新旧の型を別名で参照して逐次コピーする。変換不能なスキーマを公開前に拒否する。</summary>
internal static class TypedMigration{
    internal static string[] Describe(Type? before,Type after){
        var changes=new List<string>();var previous=new Dictionary<string,PropertyInfo>();if(before is not null){previous=Tables(before);}var next=Tables(after);
        foreach(var table in previous){
            if(!next.TryGetValue(table.Key,out var target)){throw new ManagementException("MigrationRequired","表の削除には明示的な移行・バックアップが必要です: "+table.Key);}
            var oldFields=Fields(table.Value);var newFields=Fields(target);
            foreach(var field in oldFields){if(!newFields.TryGetValue(field.Key,out var column)||field.Value.PropertyType.FullName!=column.PropertyType.FullName){throw new ManagementException("MigrationRequired","列の削除・型変更には明示的な変換が必要です: "+table.Key+"."+field.Key);}}
            foreach(var field in newFields.Where(field=>!oldFields.ContainsKey(field.Key))){if(!field.Value.PropertyType.IsValueType){throw new ManagementException("MigrationRequired","追加する参照型の列には明示的な初期値が必要です: "+table.Key+"."+field.Key);}changes.Add("列を追加: "+table.Key+"."+field.Key+"（既存行は既定値）");}
        }
        foreach(var table in next.Keys.Where(name=>!previous.ContainsKey(name))){changes.Add("空の表を追加: "+table);}
        return changes.ToArray();
    }
    private static Dictionary<string,PropertyInfo> Tables(Type type)=>type.GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(property=>property.PropertyType.IsGenericType&&(property.PropertyType.GetGenericTypeDefinition()==typeof(global::Seamless.Database.Table<,>)||property.PropertyType.GetGenericTypeDefinition()==typeof(global::Seamless.Database.Table<>))).ToDictionary(property=>property.Name,StringComparer.Ordinal);
    private static Dictionary<string,PropertyInfo> Fields(PropertyInfo table)=>table.PropertyType.GenericTypeArguments[^1].GetProperties().Where(property=>property.GetMethod is not null).ToDictionary(property=>property.Name,StringComparer.Ordinal);
    internal static void Copy(DatabaseInstance before,DatabaseInstance after,CancellationToken cancellation){
        _=Describe(before.Database.GetType(),after.Database.GetType());
        var code=new StringBuilder("extern alias previous;extern alias next;public static class Migration{public static void Copy(object source,object target,global::System.Threading.CancellationToken cancellation){var before=(previous::").Append(TypeName(before.Database.GetType())).Append(")source;var after=(next::").Append(TypeName(after.Database.GetType())).Append(")target;");
        var targets=Tables(after.Database.GetType());
        foreach(var table in Tables(before.Database.GetType())){
            var rowType=targets[table.Key].PropertyType.GenericTypeArguments[^1];var fields=Fields(table.Value);var constructor=rowType.GetConstructors().OrderByDescending(item=>item.GetParameters().Length).FirstOrDefault()??throw new ManagementException("MigrationRequired","公開された行コンストラクターが必要です。");
            code.Append("foreach(var row in before.@").Append(table.Key).Append("){cancellation.ThrowIfCancellationRequested();if(!after.@").Append(table.Key).Append(".Add(new next::").Append(TypeName(rowType)).Append('(');
            var first=true;foreach(var parameter in constructor.GetParameters()){if(!first){code.Append(',');}first=false;var property=fields.Keys.SingleOrDefault(name=>string.Equals(name,parameter.Name,StringComparison.OrdinalIgnoreCase));if(property is not null){code.Append("row.@").Append(property);}else if(parameter.ParameterType.IsValueType){code.Append("default");}else{throw new ManagementException("MigrationRequired","行コンストラクターの値が必要です: "+parameter.Name);}}
            code.Append("))){throw new global::System.InvalidOperationException(\"移行先のキーが重複します。\");}}");
        }
        code.Append("}}");var references=SourceEditor.PlatformReferences().ToList();references.Add(MetadataReference.CreateFromImage(before.Compiled.AssemblyBytes,new MetadataReferenceProperties(aliases:["previous"])));references.Add(MetadataReference.CreateFromImage(after.Compiled.AssemblyBytes,new MetadataReferenceProperties(aliases:["next"])));
        var compilation=CSharpCompilation.Create("Seamless.Migration_"+Guid.NewGuid().ToString("N"),[CSharpSyntaxTree.ParseText(code.ToString())],references,new(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release));using var pe=new MemoryStream();var result=compilation.Emit(pe,cancellationToken:cancellation);
        if(!result.Success){throw new ManagementException("MigrationRequired",string.Join("\n",result.Diagnostics.Where(item=>item.Severity==DiagnosticSeverity.Error).Select(item=>item.GetMessage())));}
        var context=new MigrationLoadContext(before.Assembly,after.Assembly);try{pe.Position=0;var assembly=context.LoadFromStream(pe);var copy=assembly.GetType("Migration")!.GetMethod("Copy")!.CreateDelegate<Action<object,object,CancellationToken>>();using var read=before.Database.BeginReadSession(cancellation);using var write=after.Database.BeginTransaction(cancellation);copy(before.Database,after.Database,cancellation);write.Commit();}finally{context.Unload();}
    }
    private static string TypeName(Type type){if(type.IsGenericType){throw new ManagementException("MigrationRequired","ジェネリックなDB・行型の移行には明示的な変換が必要です。");}return (type.FullName??throw new ManagementException("Validation","型名がありません。")).Replace('+','.');}
}
