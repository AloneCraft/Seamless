using System.Reflection;
using System.Runtime.Loader;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JoinAndGroupedTerminalsKeepLinqValuesCallbacksAndEarlyExit(bool versioned){
        const string methods="""
            private static int Note(global::System.Collections.Generic.List<int> trace,int value,int phase){trace.Add(phase*10000+value);return value;}
            public decimal JoinCount(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,row=>Note(trace,row.Id%3,1),row=>Note(trace,row.Id%3,2),(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).Count();
            public decimal JoinLongCount(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).LongCount();
            public decimal JoinSum(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Where(static value=>value%2==0).Skip(1).Take(take).Select(static value=>(decimal)value).Sum();
            public decimal JoinFirst(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).First();
            public decimal JoinDefault(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).FirstOrDefault();
            public decimal JoinAny(Tx tx,List<int> trace,int take){var any=tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).Any(static value=>value%2==0);if(any){return 1;}return 0;}
            public decimal JoinArray(Tx tx,List<int> trace,int take){var rows=tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).Where(static value=>value%2==0).Select(static value=>(decimal)value).ToArray();return rows.Sum();}
            public decimal GroupCount(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).Count();
            public decimal GroupSum(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Skip(1).Take(take).Sum(static value=>(decimal)value);
            public decimal GroupFirst(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).First();
            public decimal GroupDefault(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).FirstOrDefault();
            public decimal GroupLast(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).LastOrDefault();
            public decimal GroupAny(Tx tx,List<int> trace,int take){var any=tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).Any(static value=>value>0);if(any){return 1;}return 0;}
            public decimal GroupDictionary(Tx tx,List<int> trace,int take){var map=tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>(Key:group.Key,Total:group.Sum(static row=>row.Balance))).Take(take).ToDictionary(pair=>Note(trace,pair.Key,4),static pair=>pair.Total);return map.Values.Sum();}
            public decimal GroupList(Tx tx,List<int> trace,int take){var rows=tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).Where(static value=>value>0).Select(static value=>(decimal)value).ToList();return rows.Sum();}
            public decimal JoinOverflow(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).Select(static value=>int.MaxValue).Sum();
            public decimal JoinNullable(Tx tx,List<int> trace,int take){var total=tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).Select(static value=>value%2==0?(int?)value:null).Sum();return total.GetValueOrDefault();}
            public decimal GroupFloat(Tx tx,List<int> trace,int take){var total=tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>group.Sum(static row=>0)+(group.Key==0?100000000f:group.Key==1?1f:-100000000f)).Take(take).Sum();return (decimal)total;}
            public decimal GroupNullable(Tx tx,List<int> trace,int take){var total=tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>group.Key%2==0?(int?)group.Sum(static row=>(int)row.Balance):null).Take(take).Sum();return total.GetValueOrDefault();}
            public decimal GroupDuplicateKey(Tx tx,List<int> trace,int take){var map=tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>group.Sum(static row=>row.Balance)).Take(take).ToDictionary(value=>Note(trace,0,4),static value=>value);return map.Values.Sum();}
            private static IEqualityComparer<int> Comparer(List<int> trace){trace.Add(90000);return EqualityComparer<int>.Default;}
            public decimal JoinComparer(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3),Comparer(trace)).Take(take).Count();
            public decimal JoinNamedTuple(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).Select(static value=>(value:value,next:value+1)).Sum(static pair=>pair.value+pair.next);
            public decimal JoinLastSelect(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.Join(tx.Accounts.Rows,static row=>row.Id%3,static row=>row.Id%3,(left,right)=>Note(trace,left.Id*100+right.Id,3)).Take(take).Select(value=>Note(trace,value+1,4)).LastOrDefault();
            public decimal GroupLastSelect(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).Select(value=>Note(trace,value+1,4)).LastOrDefault();
            public decimal GroupLastWhereSelect(Tx tx,List<int> trace,int take)=>tx.Accounts.Rows.GroupBy(row=>Note(trace,row.Id%3,1)).Select(group=>Note(trace,(int)group.Sum(static row=>row.Balance),3)).Take(take).Where(value=>Note(trace,value,4)>0).Select(value=>Note(trace,value+1,5)).LastOrDefault();
            """;
        // FLinqは利用者メソッドへのC#呼出しをinterceptする。reflectionで呼ぶのはRouterだけとする。
        var wrappers=string.Join("",global::System.Text.RegularExpressions.Regex.Matches(methods,@"public decimal (\w+)\(").Select(static match=>"public decimal "+match.Groups[1].Value+"(Tx tx,List<int> trace,int take)=>new Reader()."+match.Groups[1].Value+"(tx,trace,take);"));
        var source="using System;using System.Linq;using System.Collections.Generic;using Seamless.FLinq;using Tx=Seamless.Database.Transactions.Tests.BankDatabase.TypedTransaction;namespace TerminalCase;[LinqIntercept(true)]public sealed class Reader{"+methods+"}[LinqIntercept(false)]public sealed class Reference{"+methods+"}public sealed class Router{"+wrappers+"}";
        var debug=false;
#if DEBUG
        debug=true;
#endif
        var(output,generated)=Generate(source,"TerminalCase",debug,generatedEvidencePath:Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"transaction-terminals-"+versioned+".g.cs"));
        Assert.Contains(generated,static text=>text.Contains("[取引終端]",StringComparison.Ordinal)&&text.Contains("terminal=Count",StringComparison.Ordinal)&&text.Contains("PartitionedLookupBuilder",StringComparison.Ordinal));
        var emittedMethods=generated.SelectMany(static text=>global::Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<global::Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()).ToArray();
        foreach(global::System.Text.RegularExpressions.Match match in global::System.Text.RegularExpressions.Regex.Matches(methods,@"public decimal (\w+)\(")){
            var name=match.Groups[1].Value;Assert.True(emittedMethods.Any(method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_"+name+"_",StringComparison.Ordinal)&&method.Body!.ToFullString().Contains("[取引終端]",StringComparison.Ordinal)),name+" did not use the transaction terminal.");
        }
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("terminal-case-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);var reader=assembly.GetType("TerminalCase.Router")!;var reference=assembly.GetType("TerminalCase.Reference")!;var actual=Activator.CreateInstance(reader);var expected=Activator.CreateInstance(reference);
            var options=DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,MaximumQueryWorkers=2};
            if(versioned){options=options with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=524288,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};}
            using var db=new BankDatabase(options);await using(var seed=await db.BeginTransactionAsync()){for(var id=0;id<9;id++){seed.Accounts.Add(new(id,100+id));}await seed.CommitAsync();}
            foreach(var empty in new[]{false,true}){
                await using var tx=await db.BeginTransactionAsync();tx.Accounts[1]=new(1,77);tx.Accounts.Remove(2);if(empty){foreach(var row in tx.Accounts.Rows.ToArray()){tx.Accounts.Remove(row.Id);}}
                foreach(var method in reader.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)){
                    foreach(var take in new[]{-1,0,1,2,100}){
                        var a=new List<int>();var e=new List<int>();object? av=null;object? ev=null;Type? ae=null;Type? ee=null;
                        try{ev=reference.GetMethod(method.Name)!.Invoke(expected,[tx,e,take]);}catch(TargetInvocationException error){ee=error.InnerException!.GetType();}
                        try{av=method.Invoke(actual,[tx,a,take]);}catch(TargetInvocationException error){ae=error.InnerException!.GetType();}
                        Assert.True(Equals(ev,av)&&ee==ae,$"{method.Name}, empty={empty}, take={take}: expected={ev}/{ee}, actual={av}/{ae}");Assert.True(e.SequenceEqual(a),$"{method.Name}, empty={empty}, take={take}: callback order differs. Expected [{string.Join(',',e)}], actual [{string.Join(',',a)}]");
                    }
                }
                await tx.RollbackAsync();
            }
        }finally{load.Unload();}
    }
}
