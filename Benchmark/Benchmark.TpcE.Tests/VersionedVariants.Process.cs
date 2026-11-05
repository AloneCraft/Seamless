using System.Diagnostics;
using System.Text.Json;
using Benchmark.TpcE.Core;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;

public sealed partial class VersionedVariants{
    [Theory,Trait("Category","Integration")]
    [InlineData("VL",KillMode.BeforeSave)]
    [InlineData("VL",KillMode.InLastFlush)]
    [InlineData("VL",KillMode.AfterManifest)]
    [InlineData("VL",KillMode.MidTransaction)]
    [InlineData("VF",KillMode.BeforeSave)]
    [InlineData("VF",KillMode.InLastFlush)]
    [InlineData("VF",KillMode.AfterManifest)]
    [InlineData("VF",KillMode.MidTransaction)]
    public async Task ProcessTerminationRecoversBusinessRowsAndDriverAtTheSameCommit(string name,KillMode phase){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"tpc-crash-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var directory=Path.Combine(root,"store");var options=new Core.RunOptions{Command="crash-child",Customers=12,InitialTradeDays=1,MarketDays=60,NewsItemBytes=64,Transactions=16,Warmup=0,SaveEvery=8,MemoryMegabytes=128,DiskGigabytes=1,Variants=[name]};
        var command=CrashCampaign.ResolveChildCommand();var start=new ProcessStartInfo(command.FileName){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in command.Prefix.Concat(CrashChild.ToArguments(new(options,directory,name,phase,1,1)))){start.ArgumentList.Add(argument);}
        using var process=Process.Start(start)??throw new InvalidOperationException("Could not start owned TPC worker.");var creation=process.StartTime.ToUniversalTime();
        var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));}
        finally{
            if(!process.HasExited){
                using var identity=Process.GetProcessById(process.Id);Assert.Equal(creation,identity.StartTime.ToUniversalTime());process.Kill(true);await process.WaitForExitAsync();
            }
        }
        var text=await output;var stderr=await errors;File.WriteAllText(Path.Combine(root,"worker.json"),JsonSerializer.Serialize(new{process.Id,CreationUtc=creation,phase,name,process.ExitCode,Output=text,Error=stderr}));
        Assert.NotEqual(0,process.ExitCode);Assert.Contains("@@KILL "+phase,text);Assert.True(string.IsNullOrWhiteSpace(stderr),stderr);
        long completed=0;
        foreach(var line in text.Split('\n')){if(line.StartsWith("@@TX_DONE ",StringComparison.Ordinal)){completed=long.Parse(line[10..]);}}
        var variant=VariantCatalog.ByShortName(name);var limits=new StoreLimits{MemoryBytes=128<<20,DiskBytes=1L<<30};
        using var recoveredStore=Store.Open(directory,limits);using var recovered=variant.Open(recoveredStore,options.Scale);var state=VersionedDriverState.Restore(recovered);Assert.NotNull(state);Assert.Equal(completed,state.TxnIndex);
        using var referenceStore=Store.CreateTemporary(limits);using var reference=variant.Open(referenceStore,options.Scale);
        reference.Database.BeginBulkLoad();var generation=new Core.TpcEDataGenerator(new TpcEModel(options.Seed,options.Scale)).Generate(reference.Loader);reference.Database.EndBulkLoad();
        var driver=new Driver(Driver.Initial(options.Seed,options.Scale,generation));
        while(driver.State.TxnIndex<completed){var request=driver.Next();driver.Completed(request,reference.Execute(request));}
        Assert.Empty(StateDigest.Of(reference.Database).Differences(StateDigest.Of(recovered.Database)));
        Assert.Equal(JsonSerializer.Serialize(driver.State),JsonSerializer.Serialize(state));
    }
}
