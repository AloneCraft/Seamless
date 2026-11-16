using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Database;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

// 移行(TypedMigration.Copy)の区間ごとの時間を測る計測用の試験。
// 環境変数 SEAMLESS_MIGRATION_TIMING=件数,件数,... があるときだけ動き、結果を SEAMLESS_MANAGEMENT_TEST_ROOT 配下へ書く。
[Collection("AuditStorage")]
public sealed class MigrationTimingProbe{
    private static string stage="";
    [Fact]
    public void MeasureMigrationPhases(){
        var counts=Environment.GetEnvironmentVariable("SEAMLESS_MIGRATION_TIMING");
        if(string.IsNullOrWhiteSpace(counts)){return;}
        var variants=(Environment.GetEnvironmentVariable("SEAMLESS_MIGRATION_TIMING_VARIANTS")??"manager,index,budget,memory,sized,samename,unchanged,txwrite").Split(',',StringSplitOptions.RemoveEmptyEntries);
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"migration-timing-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss"));
        Directory.CreateDirectory(root);
        var report=new StringBuilder("variant,rows,seedMs,compileColdMs,compileWarmMs,prepareMs,copyMs,completeMs,tailMs,totalCopyMs,reopenMs,fileCopyMs,fileBytes\n");
        // Roslyn の初回の重さを1回目の試行だけに押し込めないよう、最初に小さい移行を1回空回しする。
        Run(Path.Combine(root,"warmup"),"manager",1000,null);
        foreach(var n in counts.Split(',').Select(int.Parse)){
            foreach(var variant in variants){
                string line;try{line=Run(Path.Combine(root,variant+"-"+n),variant,n,report);}catch(Exception error){var inner=error is System.Reflection.TargetInvocationException{InnerException:{} cause}?cause:error;line=variant+","+n+",FAILED,"+stage+","+inner.GetType().Name+": "+inner.Message.Replace(",",";");report.AppendLine(line);}
                Console.WriteLine(line);
            }
        }
        File.WriteAllText(Path.Combine(root,"timing.csv"),report.ToString());
        Console.WriteLine("timing: "+Path.Combine(root,"timing.csv"));
    }

    // 運用の設定(16MB)で書いた保存を、メモリの上限だけ替えて開き直し、全件を読む時間を測る。
    // variant は reopen<MB>。セグメントは書いたときと同じ16MBのまま。
    private static string RunReopen(string directory,string variant,int rows,StringBuilder? report){
        var megabytes=long.Parse(variant["reopen".Length..]);
        DatabaseContainerOptions Manager(string path)=>DatabaseContainerOptions.OnStorage(path) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=1<<24,TemporaryDirectory=Path.Combine(path,"temporary")};
        var path=Path.Combine(directory,"before");
        var seed=Stopwatch.StartNew();
        using(var writer=Create(false,"Before")){
            stage="open-before";writer.Open(Manager(path));stage="seed";
            var seedMethod=writer.Database.GetType().GetMethod("Seed")!;
            for(var start=0;start<rows;start+=10_000){seedMethod.Invoke(null,[writer.Database,start,Math.Min(10_000,rows-start)]);}
        }
        seed.Stop();
        stage="reopen";var open=Stopwatch.StartNew();
        using var reader=Create(false,"Before");reader.Open(Manager(path) with{MemorySize=megabytes<<20});open.Stop();
        stage="scan";var scan=Stopwatch.StartNew();
        var count=(long)reader.Database.GetType().GetMethod("Scan")!.Invoke(null,[reader.Database])!;scan.Stop();
        Assert.Equal((long)rows,count);
        var line=string.Join(",",variant,rows,seed.ElapsedMilliseconds,0,0,0,scan.ElapsedMilliseconds,0,0,scan.ElapsedMilliseconds,open.ElapsedMilliseconds,0,0,"none");
        report?.AppendLine(line);
        return line;
    }

    private static string Run(string directory,string variant,int rows,StringBuilder? report){
        if(variant.StartsWith("reopen",StringComparison.Ordinal)){return RunReopen(directory,variant,rows,report);}
        // Manager の DatabaseInstance.Open と同じ設定(NativeRuntime.cs)。
        DatabaseContainerOptions Manager(string path)=>DatabaseContainerOptions.OnStorage(path) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=1024,PageSize=1<<20,SegmentSize=1<<24,MemorySize=1<<24,TemporaryDirectory=Path.Combine(path,"temporary")};
        var beforeOptions=Manager(Path.Combine(directory,"before"));
        // 旧側のメモリだけを大きくして読む(読み取りがディスクに落ちるかの確認)。
        if(variant=="scanmem"){beforeOptions=beforeOptions with{MemorySize=1L<<26,SegmentSize=1L<<26,ManagedMemoryLimitBytes=2L<<30};}
        var afterOptions=Manager(Path.Combine(directory,"after"));
        // 1区画64バイトに実効7件。件数の2倍の余裕を見て2の累乗へ切り上げる。メモリは全件が載る量。
        long index=(long)BitOperations.RoundUpToPowerOf2((ulong)Math.Max(1024,(long)rows*2/7*64));
        long memory=(long)BitOperations.RoundUpToPowerOf2((ulong)Math.Max(1<<24,(long)rows*128));
        if(variant is "index" or "sized"){afterOptions=afterOptions with{IndexSize=index};}
        if(variant is "memory" or "sized"){afterOptions=afterOptions with{MemorySize=memory,SegmentSize=Math.Max(1L<<24,memory)};}
        // 予算(既定256MB)を広げる。memory と sized は広げないと開けない。
        if(variant is "budget" or "memory" or "sized"){afterOptions=afterOptions with{ManagedMemoryLimitBytes=2L<<30};}
        var sameName=variant=="samename";var unchanged=variant=="unchanged";
        using var before=Create(false,sameName?"FixedSdkName":"Before");
        using var after=Create(!unchanged,sameName?"FixedSdkName":"After");
        stage="open-before";before.Open(beforeOptions);stage="open-after";after.Open(afterOptions);stage="seed";
        var seed=Stopwatch.StartNew();
        var seedMethod=before.Database.GetType().GetMethod("Seed")!;
        for(var start=0;start<rows;start+=10_000){seedMethod.Invoke(null,[before.Database,start,Math.Min(10_000,rows-start)]);}
        seed.Stop();
        var cold=Stopwatch.StartNew();_=TypedMigration.Compile(before.Database.GetType(),after.Database.GetType(),before.Compiled.AssemblyBytes,after.Compiled.AssemblyBytes,null);cold.Stop();
        var warm=Stopwatch.StartNew();_=TypedMigration.Compile(before.Database.GetType(),after.Database.GetType(),before.Compiled.AssemblyBytes,after.Compiled.AssemblyBytes,null);warm.Stop();
        var marks=new Dictionary<string,long>();var clock=Stopwatch.StartNew();
        stage="copy";
        if(variant=="txwrite"){
            // 比べる相手: 移行を通さず、新しい側へ同じ件数を取引(1万件ごとに確定)で書く。
            var afterSeed=after.Database.GetType().GetMethod("Seed")!;marks["BeforeCopy"]=clock.ElapsedTicks;
            for(var start=0;start<rows;start+=10_000){afterSeed.Invoke(null,[after.Database,start,Math.Min(10_000,rows-start)]);}
            marks["BeforeConstraints"]=clock.ElapsedTicks;marks["AfterConstraints"]=clock.ElapsedTicks;
        }else if(variant is "scan" or "scanmem"){
            // 比べる相手: 旧側を読み取りセッションで全件読むだけ。
            var scan=before.Database.GetType().GetMethod("Scan")!;marks["BeforeCopy"]=clock.ElapsedTicks;
            Assert.Equal((long)rows,(long)scan.Invoke(null,[before.Database])!);
            marks["BeforeConstraints"]=clock.ElapsedTicks;marks["AfterConstraints"]=clock.ElapsedTicks;
        }else if(variant=="bulk"){
            // 比べる相手: 旧側を読まずに、移行と同じ「未公開の版」へまとめて書くだけ。
            var bulk=after.Database.GetType().GetMethod("Bulk")!;marks["BeforeCopy"]=clock.ElapsedTicks;
            bulk.Invoke(null,[after.Database,rows]);
            marks["BeforeConstraints"]=clock.ElapsedTicks;marks["AfterConstraints"]=clock.ElapsedTicks;
        }else{TypedMigration.Copy(before,after,CancellationToken.None,null,checkpoint:(phase,_)=>{if(phase is "BeforeCopy" or "BeforeConstraints" or "AfterConstraints"&&!marks.ContainsKey(phase)){marks[phase]=clock.ElapsedTicks;}});}
        clock.Stop();
        var growth=Growth(after.Database);
        stage="verify";using(var read=after.Database.BeginReadSession()){
            var copied=((System.Collections.IEnumerable)after.Database.GetType().GetProperty("Rows")!.GetValue(after.Database)!).Cast<object>().LongCount();
            if(variant is not ("scan" or "scanmem")){Assert.Equal(rows,copied);}
        }
        // Publish はコピーの後に新しい側を閉じて開き直す。
        stage="reopen";var reopen=Stopwatch.StartNew();after.Dispose();string reopenMs;
        // 運用の設定(Manager と同じ)で開き直せるかも見る。失敗は記録して続ける。
        try{using var reopened=Create(!unchanged,sameName?"FixedSdkName":"After");reopened.Open(Manager(Path.Combine(directory,"after")));reopen.Stop();reopenMs=reopen.ElapsedMilliseconds.ToString();}
        catch(Exception error){var inner=error is System.Reflection.TargetInvocationException{InnerException:{} cause}?cause:error;reopenMs="FAIL("+inner.GetType().Name+":"+inner.Message.Replace(",",";")+")";}
        // 比べる相手: 閉じた旧側の保存フォルダーをファイルとして丸ごと複製する時間。
        before.Dispose();
        stage="filecopy";var files=Stopwatch.StartNew();var bytes=CopyDirectory(Path.Combine(directory,"before"),Path.Combine(directory,"filecopy"));files.Stop();
        double Ms(long ticks)=>ticks*1000.0/Stopwatch.Frequency;
        var prepare=Ms(marks["BeforeCopy"]);var copy=Ms(marks["BeforeConstraints"]-marks["BeforeCopy"]);
        var complete=Ms(marks["AfterConstraints"]-marks["BeforeConstraints"]);var tail=Ms(clock.ElapsedTicks-marks["AfterConstraints"]);
        var line=string.Join(",",variant,rows,seed.ElapsedMilliseconds,cold.ElapsedMilliseconds,warm.ElapsedMilliseconds,prepare.ToString("F1"),copy.ToString("F1"),complete.ToString("F1"),tail.ToString("F1"),clock.ElapsedMilliseconds,reopenMs,files.ElapsedMilliseconds,bytes,growth);
        report?.AppendLine(line);
        return line;
    }

    // 新しい側の共通ストアが記録した、索引などを広げる判断(部品ごとの最後の1件)。記録が無ければ「判断していない」。
    private static string Growth(DatabaseContainer database){
        var field=typeof(DatabaseContainer).GetField("sharedStore",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        if(field.GetValue(database) is not Seamless.Execution.Store store){return "no-store";}
        var events=store.GrowthDecisions.Values.Select(item=>item.Component+":"+item.Context.Resource+":"+item.Decision.Status+":"+item.Context.CurrentBytes+"->"+item.Decision.TargetBytes+":safe="+item.Context.IsSafePoint+":"+item.Decision.Reason).ToArray();
        return events.Length==0?"none":string.Join(" | ",events).Replace(",",";");
    }

    private static long CopyDirectory(string source,string destination){
        long bytes=0;
        foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){
            var target=Path.Combine(destination,Path.GetRelativePath(source,file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);bytes+=new FileInfo(file).Length;
        }
        return bytes;
    }

    private static DatabaseInstance Create(bool changed,string assemblyName){
        var record=changed?"public readonly record struct Row(int Id,int Value,int? Extra);":"public readonly record struct Row(int Id,int Value);";
        var create=changed?"new(id,id,null)":"new(id,id)";
        var source="using Seamless.Database;namespace Model{public sealed class Db(DatabaseContainerOptions options):DatabaseContainer(options){public Table<Key,Row> Rows=>this.Table<Key,Row>(row=>new(row.Id));protected override void EnsureTransactionSchema(){_=this.Rows;}"
            +"public static void Seed(Db db,int start,int count){using var write=db.BeginTransaction();for(var id=start;id<start+count;id++){if(!db.Rows.Add("+create+")){throw new System.InvalidOperationException();}}write.Commit();}"
            +"public static long Scan(Db db){using var read=db.BeginReadSession();long count=0;foreach(var row in db.Rows){count++;}return count;}"
            +"public static void Bulk(Db db,int count){using var snapshot=db.BeginUnpublishedSnapshot();for(var id=0;id<count;id++){snapshot.Insert(db.Rows,"+create+");}snapshot.Complete();}}"
            +"public readonly record struct Key(int Id);"+record+"}";
        var compilation=CSharpCompilation.Create(assemblyName,[CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.CSharp14))],Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release));
        using var pe=new MemoryStream();var emitted=compilation.Emit(pe);Assert.True(emitted.Success,string.Join("\n",emitted.Diagnostics));
        var bytes=pe.ToArray();var definition=new Definition("timing","Timing",DefinitionKind.Database,"Model.Db","",new("1",[new("Db.cs",source)],[]));
        return new(definition,"unused",new(bytes,new(definition.Source.Hash(),Convert.ToHexString(SHA256.HashData(bytes)),[],[])));
    }
}
