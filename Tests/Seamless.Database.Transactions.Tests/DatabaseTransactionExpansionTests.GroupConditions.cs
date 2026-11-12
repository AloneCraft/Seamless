using System.Runtime.Loader;

namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public async Task GroupedAggregatesKeepCapturedValuesAndConditionalCallbackOrder(){
        const string source="""
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Seamless.FLinq;
            using Seamless.Database;
            using Seamless.Transactions;
            using Account=Seamless.Database.Transactions.Tests.Account;
            namespace GroupedCaptureCase;
            internal static partial class DatabaseGenerator;
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){
                public Table<int,Account> Accounts=>this.Table<int,Account>(static row=>row.Id);
            }
            [LinqIntercept(true)]
            public sealed class Reader{
                private static int Note(List<int> trace,int value){trace.Add(value);return value;}
                public int[] Conditional(Database.TypedTransaction tx,List<int> trace,bool skip)=>tx.Accounts.GroupBy(static row=>0).Select(group=>skip?0:group.Sum(row=>Note(trace,row.Id))).ToArray();
                public int[] And(Database.TypedTransaction tx,List<int> trace,bool enabled)=>tx.Accounts.GroupBy(static row=>0).Select(group=>enabled&&group.Sum(row=>Note(trace,row.Id))>0?1:0).ToArray();
                public int[] Or(Database.TypedTransaction tx,List<int> trace,bool skip)=>tx.Accounts.GroupBy(static row=>0).Select(group=>skip||group.Sum(row=>Note(trace,row.Id))>0?1:0).ToArray();
                public int[] Coalesce(Database.TypedTransaction tx,List<int> trace,int? fallback)=>tx.Accounts.GroupBy(static row=>0).Select(group=>fallback??group.Sum(row=>Note(trace,row.Id))).ToArray();
                public int[] Captured(Database.TypedTransaction tx,List<int> trace,int bias)=>tx.Accounts.GroupBy(static row=>0).Select(group=>bias+group.Sum(row=>Note(trace,row.Id))).ToArray();
                public int[] ResultSelector(Database.TypedTransaction tx,List<int> trace,bool skip)=>tx.Accounts.GroupBy(static row=>0,(key,group)=>skip?0:group.Sum(row=>Note(trace,row.Id))).ToArray();
            }
            public static class Entry{
                private static void Check(int[] actual,List<int> trace,int value,bool evaluated,int[] rowOrder){
                    var expectedTrace=evaluated?rowOrder:Array.Empty<int>();
                    if(actual.Length!=1||actual[0]!=value||!trace.SequenceEqual(expectedTrace)){
                        throw new InvalidOperationException($"Aggregate result or callback order changed. value={string.Join(",",actual)}, trace={string.Join(",",trace)}");
                    }
                    trace.Clear();
                }
                public static async Task<int> Run(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{
                        TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                        IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,MaximumQueryWorkers=2,
                        TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,
                            MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
                    await using(var seed=await db.BeginTransactionAsync()){
                        for(var id=0;id<4;id++){seed.Accounts.Add(new Account(id,100));}
                        await seed.CommitAsync();
                    }
                    await using var read=await db.BeginReadOnlyTransactionAsync();
                    var reader=new Reader();var trace=new List<int>();
                    var rowOrder=read.Accounts.Rows.Select(static row=>row.Id).ToArray();
                    Check(reader.Conditional(read,trace,true),trace,0,false,rowOrder);
                    Check(reader.Conditional(read,trace,false),trace,6,true,rowOrder);
                    Check(reader.And(read,trace,false),trace,0,false,rowOrder);
                    Check(reader.And(read,trace,true),trace,1,true,rowOrder);
                    Check(reader.Or(read,trace,true),trace,1,false,rowOrder);
                    Check(reader.Or(read,trace,false),trace,1,true,rowOrder);
                    Check(reader.Coalesce(read,trace,42),trace,42,false,rowOrder);
                    Check(reader.Coalesce(read,trace,null),trace,6,true,rowOrder);
                    Check(reader.Captured(read,trace,11),trace,17,true,rowOrder);
                    Check(reader.ResultSelector(read,trace,true),trace,0,false,rowOrder);
                    Check(reader.ResultSelector(read,trace,false),trace,6,true,rowOrder);
                    await read.CommitAsync();return 6;
                }
            }
            """;
        var(output,_)=Generate(source,"GroupedCaptureCase");
        using var image=new MemoryStream();
        var emitted=output.Emit(image);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("grouped-capture-"+Guid.NewGuid(),isCollectible:true);
        load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);
            var run=assembly.GetType("GroupedCaptureCase.Entry",true)!.GetMethod("Run")!;
            Assert.Equal(6,await(Task<int>)run.Invoke(null,null)!);
        }finally{
            load.Unload();
        }
    }
}
