using System.Diagnostics;
using Seamless.Transactions;
namespace Seamless.Transactions.Tests;
internal static class ProcessEntryPoint{
    public static int Main(string[] arguments){
        if(arguments.Length==3&&arguments[0]=="metadata-ack-stop"){return MetadataRetentionTests.RunStopProbe(arguments[1],arguments[2]);}
        if(arguments.Length==3&&arguments[0] is "prepared-stop-preparation" or "prepared-stop-commit"){
            var phase=arguments[0]=="prepared-stop-preparation"?PreparedTransactionPhase.PreparationDurable:PreparedTransactionPhase.CommitDurable;
            var prepared=new PreparedTransactionJournal(arguments[1],new(){MaxFrameBytes=1024,QueueBytes=4096,SegmentBytes=16384},failureInjection:current=>{if(current==phase){Process.GetCurrentProcess().Kill();}});
            IEnumerable<WalEntry> Rows(){for(var index=0;index<80;index++){yield return new(index%2,1,WalMutation.Upsert,"process-prepared-v1",BitConverter.GetBytes(index),new byte[150]);}}
            prepared.CommitAsync(Guid.Parse(arguments[2]),Rows()).AsTask().GetAwaiter().GetResult();return 3;
        }
        if(arguments.Length!=3||arguments[0]!="commit-and-exit"){return 2;}
        var journal=new TransactionJournal(arguments[1]);
        var draft=new WalDraft(Guid.Parse(arguments[2]),[0],[new(0,1,WalMutation.Upsert,"process-stop-v1",[1],[2])],[]);
        var receipt=journal.AppendAsync(draft).AsTask().GetAwaiter().GetResult();receipt.Durable.GetAwaiter().GetResult();
        // Dispose/Stop/checkpointを通らず、確定通知を返す前にプロセスを終える。
        Environment.Exit(137);return 137;
    }
}
public sealed class ProcessTermination{
    [Theory]
    [InlineData("prepared-stop-preparation",false)]
    [InlineData("prepared-stop-commit",true)]
    public async Task PreparedTransactionSurvivesProcessKillOnlyIfFinalCommitWasDurable(string mode,bool committed){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root."),"prepared-process-"+Guid.NewGuid().ToString("N"));var id=Guid.NewGuid();
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{typeof(ProcessEntryPoint).Assembly.Location,mode,path,id.ToString("D")}){start.ArgumentList.Add(argument);}
        using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
        try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{if(!child.HasExited){child.Kill(entireProcessTree:true);}throw;}
        Assert.True(child.ExitCode!=0&&child.ExitCode!=3,(await stdout)+(await stderr));
        await using var journal=new PreparedTransactionJournal(path,new(){MaxFrameBytes=1024,QueueBytes=4096,SegmentBytes=16384});var rows=0;
        journal.Replay((transaction,lsn,metadata,entries)=>{Assert.Equal(id,transaction);foreach(var entry in entries){rows++;}});Assert.Equal(committed?80:0,rows);Assert.Equal(committed?TransactionOutcomeKind.Committed:TransactionOutcomeKind.Unknown,journal.GetOutcome(id).Kind);
    }
    [Fact]public async Task LostCommitNotificationIsQueryableAfterProcessExitAndWalPruning(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.ProcessStop",Guid.NewGuid().ToString("N"));var id=Guid.NewGuid();
        try{
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var argument in new[]{typeof(ProcessEntryPoint).Assembly.Location,"commit-and-exit",path,id.ToString("D")}){start.ArgumentList.Add(argument);}
            using var child=Process.Start(start)!;
            var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{if(!child.HasExited){child.Kill(entireProcessTree:true);}throw;}
            Assert.True(child.ExitCode==137,(await stdout)+(await stderr));
            for(var epoch=0;epoch<3;epoch++){
                await using var journal=new TransactionJournal(path);Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);await journal.CheckpointAndPruneAsync(1);
            }
            Assert.Equal(2,Directory.GetFiles(path,"segment-*.wal").Length);
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
}
