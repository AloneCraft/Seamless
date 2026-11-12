using System.Collections;
using Seamless.Database;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Execution;

namespace Management.Tests;

public sealed partial class SameNameMigration{
    [Fact]
    public void CustomValuesKeepPrivateFieldsAndReferenceGraphAcrossGenerations(){
        using var before=Create(false,CustomRecords(false));using var after=Create(true,CustomRecords(true));
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"custom-"+Guid.NewGuid().ToString("N"));
        OpenCustom(before,Path.Combine(root,"before"),false,true);OpenCustom(after,Path.Combine(root,"after"),false,true);
        AddCustomRow(before);
        TypedMigration.Copy(before,after,CancellationToken.None);
        using var read=after.Database.BeginReadSession();var row=Assert.Single(CustomRows(after));
        var value=Property(row,"Value")!;var node=Property(value,"Node")!;
        Assert.Same(after.Assembly,node.GetType().Assembly);Assert.Equal(17,Property(node,"Number"));
        Assert.Same(node,node.GetType().GetField("Next")!.GetValue(node));
        Assert.Same(node,Property(row,"Shared"));Assert.Same(node,Property(Property(row,"Optional")!,"Node"));
        Assert.Same(node,Property(((IList)Property(row,"Values")!)[0]!,"Node"));
        Assert.Equal(2,Convert.ToInt32(Property(value,"Kind")));Assert.Null(Property(row,"Extra"));
        using var previousRead=before.Database.BeginReadSession();var previous=Assert.Single(CustomRows(before));
        Assert.Same(before.Assembly,Property(previous,"Shared")!.GetType().Assembly);

        var converter=new MigrationValueConverter(before.Assembly,after.Assembly);
        var parameters=after.Assembly.GetType("Model.Row")!.GetConstructors().Single().GetParameters().Select(parameter=>parameter.ParameterType).ToArray();
        foreach(var parameter in parameters){converter.Prepare(parameter);}
        object[] values=[Property(previous,"Id")!,Property(previous,"Value")!,Property(previous,"Shared")!,Property(previous,"Optional")!,Property(previous,"Values")!,null!];
        var small=new ResourceBudget(1024,0);
        Assert.Throws<ResourceLimitException>(()=>converter.ConvertRows([values],parameters,small,CancellationToken.None).ToArray());Assert.Equal(0,small.MemoryBytes);
        var budget=new ResourceBudget(1<<20,0);using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>converter.ConvertRows([values],parameters,budget,cancellation.Token).ToArray());Assert.Equal(0,budget.MemoryBytes);
        using(var enumerator=converter.ConvertRows([values],parameters,budget,CancellationToken.None).GetEnumerator()){
            Assert.True(enumerator.MoveNext());Assert.True(budget.MemoryBytes>0);
        }
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]
    public void ChangedCustomFieldContractIsRejectedBeforeCopy(){
        using var before=Create(false,CustomRecords(false));using var after=Create(true,CustomRecords(true).Replace("private readonly int number","private readonly long number"));
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"custom-shape-"+Guid.NewGuid().ToString("N"));
        OpenCustom(before,Path.Combine(root,"before"),false,true);OpenCustom(after,Path.Combine(root,"after"),false,true);AddCustomRow(before);
        Assert.Equal("MigrationRequired",Assert.Throws<ManagementException>(()=>TypedMigration.Copy(before,after,CancellationToken.None)).Code);
        using var read=before.Database.BeginReadSession();Assert.Equal(17,Property(Property(Assert.Single(CustomRows(before)),"Shared")!,"Number"));
        using var targetRead=after.Database.BeginReadSession();Assert.Empty(CustomRows(after));
    }
    private static string CustomRecords(bool after){
        var extra="";if(after){extra=",int? Extra";}
        return "public enum Kind:short{First=1,Second=2}"+
            "public sealed class Node{private readonly int number;public int Number=>(int)this.number;public Node Next;public Node(int number){this.number=number;}}"+
            "public readonly record struct Payload(Kind Kind,Node Node);"+
            "public readonly record struct Row(int Id,Payload Value,Node Shared,Payload? Optional,System.Collections.Generic.List<Payload> Values"+extra+");";
    }
    [Theory][InlineData(false,"FixedSdkName")][InlineData(true,"FixedSdkName")][InlineData(false,"ChangedSdkName")][InlineData(true,"ChangedSdkName")]
    public void CustomStructEnumNullableAndArrayCrossBothCopyPaths(bool canonical,string assemblyName){
        const string declarations="public enum Kind:short{First=1,Second=2}public readonly record struct Payload(Kind Kind,int Number);public class Outer<T>{public readonly record struct Inner<U>(T First,U Second);}";
        using var before=Create(false,declarations+"public readonly record struct Row(int Id,Payload Value,Payload? Optional,Payload[] Values,Outer<int>.Inner<Payload> Generic);");
        using var after=Create(true,declarations+"public readonly record struct Row(int Id,Payload Value,Payload? Optional,Payload[] Values,Outer<int>.Inner<Payload> Generic,int? Extra);",assemblyName);
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"custom-value-"+Guid.NewGuid().ToString("N"));
        OpenCustom(before,Path.Combine(root,"before"),canonical,!canonical);OpenCustom(after,Path.Combine(root,"after"),canonical,!canonical);
        var payloadType=before.Assembly.GetType("Model.Payload")!;var payload=Activator.CreateInstance(payloadType,[Enum.ToObject(before.Assembly.GetType("Model.Kind")!,2),17])!;
        var array=Array.CreateInstance(payloadType,1);array.SetValue(payload,0);var rowType=before.Assembly.GetType("Model.Row")!;
        var generic=Activator.CreateInstance(before.Assembly.GetType("Model.Outer`1+Inner`1")!.MakeGenericType(typeof(int),payloadType),[3,payload])!;
        using(var write=before.Database.BeginTransaction()){
            var table=Property(before.Database,"Rows")!;Assert.Equal(true,table.GetType().GetMethod("Add",[rowType])!.Invoke(table,[Activator.CreateInstance(rowType,[1,payload,payload,array,generic])]));write.Commit();
        }
        TypedMigration.Copy(before,after,CancellationToken.None);
        using var read=after.Database.BeginReadSession();var row=Assert.Single(CustomRows(after));var value=Property(row,"Value")!;
        Assert.Same(after.Assembly,value.GetType().Assembly);Assert.Equal(17,Property(value,"Number"));Assert.Equal(2,Convert.ToInt32(Property(value,"Kind")));
        Assert.Equal(17,Property(Property(row,"Optional")!,"Number"));Assert.Equal(17,Property(((Array)Property(row,"Values")!).GetValue(0)!,"Number"));Assert.Null(Property(row,"Extra"));
        Assert.Equal(17,Property(Property(Property(row,"Generic")!,"Second")!,"Number"));
        var matrix=Array.CreateInstance(payloadType,[1,2],[-1,3]);matrix.SetValue(payload,-1,3);matrix.SetValue(payload,-1,4);
        var converter=new MigrationValueConverter(before.Assembly,after.Assembly);var targetArray=after.Assembly.GetType("Model.Payload")!.MakeArrayType(2);converter.Prepare(targetArray);
        var budget=new ResourceBudget(1<<20,0);
        var copied=(Array)Assert.Single(converter.ConvertRows([[matrix]],[targetArray],budget,CancellationToken.None))[0];
        Assert.Equal(-1,copied.GetLowerBound(0));Assert.Equal(3,copied.GetLowerBound(1));Assert.Equal(2,copied.Length);
        Assert.Same(after.Assembly,copied.GetValue(-1,4)!.GetType().Assembly);Assert.Equal(17,Property(copied.GetValue(-1,4)!,"Number"));Assert.Equal(0,budget.MemoryBytes);
    }
    private static void OpenCustom(DatabaseInstance instance,string root,bool canonical,bool graph=false){
        var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(root,"temp")};
        if(canonical){options=options with{StorageMode=DatabaseStorageMode.Storage,StorageDirectory=root,RecordSerialization=DatabaseRecordSerialization.CanonicalV1};}
        if(graph){options=options with{StorageMode=DatabaseStorageMode.Storage,StorageDirectory=root,RecordSerialization=DatabaseRecordSerialization.RecordGraphV1};}
        instance.Open(options);
    }
    private static void AddCustomRow(DatabaseInstance instance){
        var assembly=instance.Assembly;var nodeType=assembly.GetType("Model.Node")!;var payloadType=assembly.GetType("Model.Payload")!;
        var node=Activator.CreateInstance(nodeType,[17])!;nodeType.GetField("Next")!.SetValue(node,node);
        var payload=Activator.CreateInstance(payloadType,[Enum.ToObject(assembly.GetType("Model.Kind")!,2),node])!;
        var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(payloadType))!;list.Add(payload);
        var rowType=assembly.GetType("Model.Row")!;var row=Activator.CreateInstance(rowType,[1,payload,node,payload,list]);
        var table=Property(instance.Database,"Rows")!;
        using var transaction=instance.Database.BeginTransaction();Assert.Equal(true,table.GetType().GetMethod("Add",[rowType])!.Invoke(table,[row]));transaction.Commit();
    }
    private static object? Property(object value,string name)=>value.GetType().GetProperty(name)!.GetValue(value);
    private static IEnumerable<object> CustomRows(DatabaseInstance instance)=>((IEnumerable)Property(instance.Database,"Rows")!).Cast<object>();
}
