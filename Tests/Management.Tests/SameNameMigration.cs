using System.Collections;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Database;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed partial class SameNameMigration{
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public void SharedColumnsAndNullableDefaultsCrossSameNamedAssemblies(bool canonical,bool transform)=>Run(canonical,transform,null);
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public void FailureAndCancellationKeepSource(bool canonical,bool cancel)=>Run(canonical,false,cancel);
    private static void Run(bool canonical,bool transform,bool? cancel){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"same-"+Guid.NewGuid().ToString("N"));
        var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(root,"temp")};
        if(canonical){options=DatabaseContainerOptions.OnStorage(root) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(root,"temp")};}
        var beforeOptions=options;var afterOptions=options;
        if(canonical){beforeOptions=options with{StorageDirectory=Path.Combine(root,"before")};afterOptions=options with{StorageDirectory=Path.Combine(root,"after")};}
        using var before=Create(false);using var after=Create(true);
        before.Open(beforeOptions);after.Open(afterOptions);
        Assert.Equal(before.Assembly.FullName,after.Assembly.FullName);Assert.NotEqual(before.Assembly.ManifestModule.ModuleVersionId,after.Assembly.ManifestModule.ModuleVersionId);
        var oldTable=before.Database.GetType().GetProperty("Rows")!.GetValue(before.Database)!;var oldRow=before.Assembly.GetType("Model.Row")!;
        using(var write=before.Database.BeginTransaction()){
            Assert.Equal(true,oldTable.GetType().GetMethod("Add",[oldRow])!.Invoke(oldTable,[Activator.CreateInstance(oldRow,[1,9])]));
            Assert.Equal(true,oldTable.GetType().GetMethod("Add",[oldRow])!.Invoke(oldTable,[Activator.CreateInstance(oldRow,[2,10])]));
            write.Commit();
        }
        MigrationDefinition? migration=null;
        if(transform){migration=new(1,[new("Rows","Rows",[new("Value","Value","row.Value*2")],[])],[]);}
        if(cancel.HasValue){
            using var cancellation=new CancellationTokenSource();
            if(!cancel.Value){migration=new(1,[new("Rows","Rows",[new("Id","Id","0")],[])],[]);}
            var failure=Record.Exception(()=>TypedMigration.Copy(before,after,cancellation.Token,migration,checkpoint:(phase,count)=>{if(cancel.Value&&count==1){cancellation.Cancel();}}));
            if(cancel.Value){Assert.IsType<OperationCanceledException>(failure);}
            else{Assert.True(failure is InvalidOperationException or InvalidDataException,failure?.ToString());}
            using var previousRead=before.Database.BeginReadSession();
            var previousRows=((IEnumerable)oldTable).Cast<object>().OrderBy(row=>(int)oldRow.GetProperty("Id")!.GetValue(row)!).ToArray();
            Assert.Equal(new[]{9,10},previousRows.Select(row=>(int)oldRow.GetProperty("Value")!.GetValue(row)!));
            if(!canonical){using var targetRead=after.Database.BeginReadSession();Assert.Empty((IEnumerable)after.Database.GetType().GetProperty("Rows")!.GetValue(after.Database)!);}
            return;
        }
        TypedMigration.Copy(before,after,CancellationToken.None,migration);
        using var read=after.Database.BeginReadSession();
        var rows=((IEnumerable)after.Database.GetType().GetProperty("Rows")!.GetValue(after.Database)!).Cast<object>().OrderBy(row=>(int)row.GetType().GetProperty("Id")!.GetValue(row)!).ToArray();
        var expected=new[]{9,10};if(transform){expected=[18,20];}
        Assert.Equal(expected,rows.Select(row=>(int)row.GetType().GetProperty("Value")!.GetValue(row)!));
        Assert.All(rows,row=>Assert.Null(row.GetType().GetProperty("Extra")!.GetValue(row)));
    }
    private static DatabaseInstance Create(bool after,string? records=null,string assemblyName="FixedSdkName"){
        var record="public readonly record struct Row(int Id,int Value);";
        if(after){record="public readonly record struct Row(int Id,int Value,int? Extra);";}
        var source="using Seamless.Database;namespace Model{public sealed class Db(DatabaseContainerOptions options):DatabaseContainer(options){public Table<Key,Row> Rows=>this.Table<Key,Row>(row=>new(row.Id));protected override void EnsureTransactionSchema(){_=this.Rows;}}public readonly record struct Key(int Id);"+(records??record)+"}";
        var compilation=CSharpCompilation.Create(assemblyName,[CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.CSharp14))],Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary));
        using var pe=new MemoryStream();var emitted=compilation.Emit(pe);Assert.True(emitted.Success,string.Join("\n",emitted.Diagnostics));
        var bytes=pe.ToArray();var definition=new Definition("same","Same",DefinitionKind.Database,"Model.Db","",new("1",[new("Db.cs",source)],[]));
        return new(definition,"unused",new(bytes,new(definition.Source.Hash(),Convert.ToHexString(SHA256.HashData(bytes)),[],[])));
    }
}
