using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Database;

namespace Seamless.Management.Server;

/// <summary>全表の最終形へ逐次変換し、取引の最終状態で制約を検査する。</summary>
internal static partial class TypedMigration{
    internal sealed record CompiledMigration(byte[] Assembly,byte[] Symbols,string Source);
    private sealed record TablePlan(DatabaseTableDeclaration Before,DatabaseTableDeclaration After,ConstructorInfo Constructor,string[] Values);
    private static (TablePlan[] Tables,string[] Changes) Plan(Type? before,Type after,MigrationDefinition? migration){
        if(migration is not null&&migration.Version!=1){throw new ManagementException("Unsupported","未対応の移行定義です。");}
        var previous=new Dictionary<string,DatabaseTableDeclaration>(StringComparer.Ordinal);if(before is not null){previous=DatabaseTableDeclaration.Read(before).ToDictionary(table=>table.Path,StringComparer.Ordinal);}
        var next=DatabaseTableDeclaration.Read(after).ToDictionary(table=>table.Path,StringComparer.Ordinal);
        var mappings=migration?.Tables??[];var dropped=migration?.DroppedTables??[];
        if(mappings.Select(item=>item.Target).Distinct().Count()!=mappings.Length||mappings.Select(item=>item.Source).Distinct().Count()!=mappings.Length||dropped.Distinct().Count()!=dropped.Length){throw new ManagementException("MigrationRequired","表の移行指定が重複しています。");}
        if(mappings.Any(item=>!next.ContainsKey(item.Target)||!previous.ContainsKey(item.Source))||dropped.Any(name=>!previous.ContainsKey(name))){throw new ManagementException("MigrationRequired","表の移行指定に存在しない表があります。");}
        var consumed=new HashSet<string>(StringComparer.Ordinal);var plans=new List<TablePlan>();var changes=new List<string>();
        foreach(var table in next.Values){
            var mapping=mappings.SingleOrDefault(item=>item.Target==table.Path);var source=mapping?.Source??table.Path;
            if(!previous.TryGetValue(source,out var old)){changes.Add("空の表を追加: "+table.Path);continue;}
            if(!consumed.Add(source)||dropped.Contains(source)){throw new ManagementException("MigrationRequired","表の移行元または削除指定が競合しています: "+source);}
            if(source!=table.Path){changes.Add("表の名前を変更: "+source+" → "+table.Path);}
            var oldFields=Fields(old);var newFields=Fields(table);var columns=mapping?.Columns??[];var removed=mapping?.DroppedColumns??[];
            if(columns.Select(item=>item.Target).Distinct().Count()!=columns.Length||columns.Any(item=>!newFields.ContainsKey(item.Target))||removed.Any(name=>!oldFields.ContainsKey(name))){throw new ManagementException("MigrationRequired","列の移行指定が不正です: "+table.Path);}
            var used=new HashSet<string>(StringComparer.Ordinal);var values=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var field in newFields){
                var column=columns.SingleOrDefault(item=>item.Target==field.Key);var from=column?.Source??field.Key;
                if(column?.Expression is{} expression){
                    var syntax=SyntaxFactory.ParseExpression(expression);if(syntax.ContainsDiagnostics||syntax.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(node=>node.Identifier.ValueText is "before" or "after" or "source" or "target" or "cancellation" or "checkpoint" or "snapshot")){throw new ManagementException("MigrationRequired","値の変換式が不正です: "+table.Path+"."+field.Key);}
                    values.Add(field.Key,expression);if(column.Source is not null){if(!oldFields.ContainsKey(column.Source)){throw new ManagementException("MigrationRequired","変換元の列がありません: "+column.Source);}used.Add(column.Source);}else if(oldFields.ContainsKey(field.Key)){used.Add(field.Key);}
                    changes.Add("値を変換: "+table.Path+"."+field.Key);continue;
                }
                if(oldFields.TryGetValue(from,out var oldField)){
                    if(!MigrationValueConverter.SameTypeName(oldField.PropertyType,field.Value.PropertyType,before!.Assembly,after.Assembly)){throw new ManagementException("MigrationRequired","列の型変更には明示的な変換式が必要です: "+table.Path+"."+field.Key);}
                    used.Add(from);values.Add(field.Key,"row.@"+from);if(from!=field.Key){changes.Add("列の名前を変更: "+table.Path+"."+from+" → "+field.Key);}continue;
                }
                if(!field.Value.PropertyType.IsValueType){throw new ManagementException("MigrationRequired","追加列の初期値を指定してください: "+table.Path+"."+field.Key);}
                values.Add(field.Key,"default");changes.Add("列を追加: "+table.Path+"."+field.Key+"（既存行は既定値）");
            }
            foreach(var field in oldFields.Keys.Where(name=>!used.Contains(name))){if(!removed.Contains(field)){throw new ManagementException("MigrationRequired","列の削除または名前変更を明示してください: "+source+"."+field);}changes.Add("データを削除: "+source+"."+field);}
            if(removed.Any(used.Contains)){throw new ManagementException("MigrationRequired","使用中の列を削除指定しています: "+source);}
            var rowType=table.Property.PropertyType.GenericTypeArguments[^1];var constructor=rowType.GetConstructors().OrderByDescending(item=>item.GetParameters().Length).FirstOrDefault()??throw new ManagementException("MigrationRequired","公開された行コンストラクターが必要です。");
            var parameters=constructor.GetParameters();var members=constructor.GetCustomAttribute<GeneratedRecordConstructorAttribute>()?.Members??parameters.Select(parameter=>parameter.Name??"").ToArray();
            if(members.Count!=parameters.Length||members.Any(name=>!values.Keys.Any(field=>field.Equals(name,StringComparison.OrdinalIgnoreCase)))){throw new ManagementException("MigrationRequired","行コンストラクターと列の対応を指定してください: "+table.Path);}
            var arguments=members.Select(name=>values.Single(pair=>pair.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Value).ToArray();plans.Add(new(old,table,constructor,arguments));
        }
        foreach(var table in previous.Keys.Where(name=>!consumed.Contains(name))){if(!dropped.Contains(table)){throw new ManagementException("MigrationRequired","表の削除または名前変更を明示してください: "+table);}changes.Add("表のデータを削除: "+table);}
        return(plans.ToArray(),changes.ToArray());
    }
    internal static string[] Describe(Type? before,Type after,MigrationDefinition? migration=null)=>Plan(before,after,migration).Changes;
    private static Dictionary<string,PropertyInfo> Fields(DatabaseTableDeclaration table)=>table.Property.PropertyType.GenericTypeArguments[^1].GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(field=>field.GetIndexParameters().Length==0).ToDictionary(field=>field.Name,StringComparer.Ordinal);
    internal static CompiledMigration Compile(Type before,Type after,byte[] beforeBytes,byte[] afterBytes,MigrationDefinition? migration,CapturedSourceBuild? beforeBuild=null){
        var plan=Plan(before,after,migration);
        if(UseValueBridge(before,after,plan.Tables)){return CompileReader(before,after,beforeBytes,plan.Tables,beforeBuild);}
        var code=new StringBuilder("extern alias previous;extern alias next;public static class Migration{public static void Copy(object source,object target,global::System.Threading.CancellationToken cancellation,global::System.Action<string,long> checkpoint,global::Seamless.Database.DatabaseContainer.UnpublishedSnapshotBuilder snapshot){var before=(previous::").Append(TypeName(before)).Append(")source;var after=(next::").Append(TypeName(after)).Append(")target;long count=0;");
        foreach(var table in plan.Tables){
            var value="new next::"+TypeName(table.After.Property.PropertyType.GenericTypeArguments[^1])+"("+string.Join(",",table.Values)+")";var target="after."+Access(table.After.Path);
            code.Append("foreach(var row in before.").Append(Access(table.Before.Path)).Append("){cancellation.ThrowIfCancellationRequested();if(snapshot is not null){snapshot.Insert(").Append(target).Append(',').Append(value).Append(");}else if(!").Append(target).Append(".Add(").Append(value).Append(")){throw new global::System.InvalidOperationException(\"移行先のキーが重複します。\");}count++;checkpoint?.Invoke(").Append(SyntaxFactory.Literal(table.After.Path).Text).Append(",count);}");
        }
        code.Append("}}");var references=SourceEditor.PlatformReferences().ToList();references.Add(MetadataReference.CreateFromImage(beforeBytes,new MetadataReferenceProperties(aliases:["previous"])));references.Add(MetadataReference.CreateFromImage(afterBytes,new MetadataReferenceProperties(aliases:["next"])));
        return Emit(code,references);
    }
    private static CompiledMigration Emit(StringBuilder code,IEnumerable<MetadataReference> references){
        var source=SyntaxFactory.ParseCompilationUnit(code.ToString()).NormalizeWhitespace(eol:"\n").ToFullString();var sourceText=Microsoft.CodeAnalysis.Text.SourceText.From(source,Encoding.UTF8);
        var tree=CSharpSyntaxTree.ParseText(sourceText,path:"Migration.g.cs");var compilation=CSharpCompilation.Create("Seamless.Migration_"+Guid.NewGuid().ToString("N"),[tree],references,new(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Debug));
        var policy=RuntimeCompiler.ValidatePolicy(compilation,[tree]);if(policy.Length>0){throw new ManagementException("ManagementCodePolicy",string.Join("\n",policy.Select(item=>item.Message)));}
        using var pe=new MemoryStream();using var pdb=new MemoryStream();var result=compilation.Emit(pe,pdb,options:new Microsoft.CodeAnalysis.Emit.EmitOptions(debugInformationFormat:Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb),embeddedTexts:[EmbeddedText.FromSource(tree.FilePath,sourceText)]);if(!result.Success){throw new ManagementException("MigrationRequired",string.Join("\n",result.Diagnostics.Where(item=>item.Severity==DiagnosticSeverity.Error).Select(item=>item.GetMessage())));}return new(pe.ToArray(),pdb.ToArray(),source);
    }
    internal static void Copy(DatabaseInstance before,DatabaseInstance after,CancellationToken cancellation,MigrationDefinition? migration=null,Action<string,long>? checkpoint=null,Action<string>? sourceGenerated=null){
        var bytes=Compile(before.Database.GetType(),after.Database.GetType(),before.Compiled.AssemblyBytes,after.Compiled.AssemblyBytes,migration,before.Compiled.ImportedBuild);var context=new MigrationLoadContext(before.Assembly,after.Assembly);
        sourceGenerated?.Invoke(bytes.Source);
        try{using var pe=new MemoryStream(bytes.Assembly);using var pdb=new MemoryStream(bytes.Symbols);var assembly=context.LoadFromStream(pe,pdb);
            Action<object,object,CancellationToken,Action<string,long>?,DatabaseContainer.UnpublishedSnapshotBuilder?> copy;
            if(UseValueBridge(before.Database.GetType(),after.Database.GetType(),Plan(before.Database.GetType(),after.Database.GetType(),migration).Tables)){copy=CreateSharedCopy(assembly,before,after,migration);}
            else{copy=assembly.GetType("Migration")!.GetMethod("Copy")!.CreateDelegate<Action<object,object,CancellationToken,Action<string,long>?,DatabaseContainer.UnpublishedSnapshotBuilder?>>();}
            using var read=before.Database.BeginReadSession(cancellation);
            if(after.Database.Options.RecordSerialization==DatabaseRecordSerialization.CanonicalV1){
                using var snapshot=after.Database.BeginUnpublishedSnapshot();checkpoint?.Invoke("BeforeCopy",0);copy(before.Database,after.Database,cancellation,checkpoint,snapshot);checkpoint?.Invoke("BeforeConstraints",0);cancellation.ThrowIfCancellationRequested();snapshot.Complete();checkpoint?.Invoke("AfterConstraints",0);cancellation.ThrowIfCancellationRequested();
            }else{
                using var write=after.Database.BeginTransaction(cancellation);checkpoint?.Invoke("BeforeCopy",0);copy(before.Database,after.Database,cancellation,checkpoint,null);checkpoint?.Invoke("BeforeConstraints",0);after.Database.ValidateRelations();checkpoint?.Invoke("AfterConstraints",0);cancellation.ThrowIfCancellationRequested();write.Commit();
            }
        }finally{context.Unload();}
    }
    private static string Access(string path)=>string.Join(".",path.Split('.').Select(name=>"@"+name));
    private static string TypeName(Type type){if(type.IsGenericType){throw new ManagementException("MigrationRequired","ジェネリックなDB・行型の移行には明示的な変換が必要です。");}return (type.FullName??throw new ManagementException("Validation","型名がありません。")).Replace('+','.');}
}
