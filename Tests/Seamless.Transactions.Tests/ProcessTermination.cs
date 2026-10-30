using System.Diagnostics;
using Seamless.Transactions;
namespace Seamless.Transactions.Tests;
internal static class ProcessEntryPoint{
    public static int Main(string[] arguments){
        if(arguments.Length!=3||arguments[0]!="commit-and-exit"){return 2;}
        var journal=new TransactionJournal(arguments[1]);
        var draft=new WalDraft(Guid.Parse(arguments[2]),[0],[new(0,1,WalMutation.Upsert,"process-stop-v1",[1],[2])],[]);
        var receipt=journal.AppendAsync(draft).AsTask().GetAwaiter().GetResult();receipt.Durable.GetAwaiter().GetResult();
        // Dispose/Stop/checkpointを通らず、確定通知を返す前にプロセスを終える。
        Environment.Exit(137);return 137;
    }
}
public sealed class ProcessTermination{
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
