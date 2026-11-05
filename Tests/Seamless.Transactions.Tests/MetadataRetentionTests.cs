using Seamless.Transactions;

namespace Seamless.Transactions.Tests;

public sealed class MetadataRetentionTests{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated root."),"metadata-retention-"+Guid.NewGuid().ToString("N"));
    internal static WalOptions Options()=>new(){MaxFrameBytes=4096,QueueBytes=16384,SegmentBytes=16384,CatalogIdentity="metadata-retention-tests",MetadataConsumer="audit-v1"};
    private static async Task<long> Append(TransactionJournal journal,Guid? id=null){var result=await journal.AppendAsync(new(id??Guid.NewGuid(),[0],[new(0,1,WalMutation.Upsert,"test-v1",[1],new byte[400])],new byte[700]));await result.Durable;return result.Lsn;}
    [Fact]public async Task AcknowledgedPositionPersistsWithWalAndConsumerIdentity(){
        var root=Root();var id=Guid.NewGuid();await using(var journal=new TransactionJournal(root,Options())){var lsn=await Append(journal,id);journal.AcknowledgeMetadataThrough(lsn);Assert.Equal(lsn,journal.MetadataAcknowledgedLsn);var frame=Assert.Single(journal.ReadTransactions());Assert.Equal(id,frame.Draft.Id);Assert.Single(frame.Draft.Entries);Assert.Equal(700,frame.Draft.Metadata.Length);}
        await using(var reopened=new TransactionJournal(root,Options())){Assert.Equal(1,reopened.MetadataAcknowledgedLsn);}
        Assert.Throws<InvalidDataException>(()=>new TransactionJournal(root,Options() with{MetadataConsumer=null}));Assert.Throws<InvalidDataException>(()=>new TransactionJournal(root,Options() with{MetadataConsumer="other"}));await using var correct=new TransactionJournal(root,Options());Assert.Equal(1,correct.MetadataAcknowledgedLsn);
    }
    [Fact]public async Task UnacknowledgedMetadataSurvivesCheckpointsAndReopens(){
        var root=Root();await using(var journal=new TransactionJournal(root,Options())){for(var i=0;i<32;i++){await Append(journal);}await journal.CheckpointAndPruneAsync(32);}
        for(var epoch=0;epoch<4;epoch++){await using var journal=new TransactionJournal(root,Options());Assert.Equal(1,journal.Device.FirstAvailableLsn);Assert.Equal(32,journal.ReadTransactions().Count());await journal.CheckpointAndPruneAsync(32);}
        await using(var journal=new TransactionJournal(root,Options())){journal.AcknowledgeMetadataThrough(32);await journal.CheckpointAndPruneAsync(32);}for(var epoch=0;epoch<3;epoch++){await using var journal=new TransactionJournal(root,Options());await journal.CheckpointAndPruneAsync(32);Assert.Equal(32,journal.MetadataAcknowledgedLsn);if(epoch==2){Assert.True(journal.Device.FirstAvailableLsn>1);}}
    }
    [Fact]public async Task FailedPositionPublicationKeepsTheOldFenceAndCanBeRetried(){
        var root=Root();await using(var journal=new TransactionJournal(root,Options())){await Append(journal);journal.MetadataCheckpoint=phase=>{if(phase=="Written"){throw new IOException("injected ack failure");}};Assert.Throws<IOException>(()=>journal.AcknowledgeMetadataThrough(1));Assert.Equal(0,journal.MetadataAcknowledgedLsn);Assert.Null(journal.Writer.Failure.Error);journal.MetadataCheckpoint=null;journal.AcknowledgeMetadataThrough(1);Assert.Equal(1,journal.MetadataAcknowledgedLsn);}
        await using var reopened=new TransactionJournal(root,Options());Assert.Equal(1,reopened.MetadataAcknowledgedLsn);
    }
    [Fact]public async Task FailureAfterPublicationDoesNotRegressTheDurablePosition(){
        var root=Root();await using(var journal=new TransactionJournal(root,Options())){await Append(journal);await Append(journal);journal.MetadataCheckpoint=phase=>{if(phase=="Published"){throw new IOException("injected response loss");}};Assert.Throws<IOException>(()=>journal.AcknowledgeMetadataThrough(2));Assert.Equal(2,journal.MetadataAcknowledgedLsn);Assert.Throws<ArgumentOutOfRangeException>(()=>journal.AcknowledgeMetadataThrough(1));Assert.Null(journal.Writer.Failure.Error);}await using var reopened=new TransactionJournal(root,Options());Assert.Equal(2,reopened.MetadataAcknowledgedLsn);
    }
    [Fact]public async Task MissingOrCorruptPositionIsNotResetAfterDataWasCommitted(){
        var root=Root();await using(var journal=new TransactionJournal(root,Options())){await Append(journal);}var path=Path.Combine(root,"metadata-ack.v1");var original=File.ReadAllBytes(path);File.WriteAllBytes(path,[1,2,3]);Assert.Throws<InvalidDataException>(()=>new TransactionJournal(root,Options()));File.Delete(path);Assert.Throws<InvalidDataException>(()=>new TransactionJournal(root,Options()));File.WriteAllBytes(path,original);await using var restored=new TransactionJournal(root,Options());Assert.Equal(0,restored.MetadataAcknowledgedLsn);Assert.Single(restored.ReadTransactions());
    }
    [Fact]public async Task AcknowledgementCannotMoveBackwardsOrPastDurableData(){
        await using var journal=new TransactionJournal(Root(),Options());Assert.Throws<ArgumentOutOfRangeException>(()=>journal.AcknowledgeMetadataThrough(1));await Append(journal);journal.AcknowledgeMetadataThrough(1);journal.AcknowledgeMetadataThrough(1);Assert.Throws<ArgumentOutOfRangeException>(()=>journal.AcknowledgeMetadataThrough(0));Assert.Throws<ArgumentOutOfRangeException>(()=>journal.AcknowledgeMetadataThrough(2));
    }
    [Fact]public async Task DefaultProfileDoesNotCreateAMetadataPosition(){var root=Root();await using(var journal=new TransactionJournal(root)){Assert.Throws<NotSupportedException>(()=>journal.AcknowledgeMetadataThrough(0));Assert.False(File.Exists(Path.Combine(root,"metadata-ack.v1")));}using var original=new WalFileDevice(root);Assert.Equal(0,original.RecoveredLsn);}
    [Fact]public async Task ExistingProfileIsNotConvertedByAddingAConsumerOption(){var root=Root();await using(var original=new TransactionJournal(root,Options() with{MetadataConsumer=null})){await Append(original);}Assert.Throws<InvalidDataException>(()=>new TransactionJournal(root,Options()));await using var reopened=new TransactionJournal(root,Options() with{MetadataConsumer=null});Assert.Single(reopened.ReadTransactions());Assert.False(File.Exists(Path.Combine(root,"metadata-ack.v1")));}
    [Fact]public async Task PartialAcknowledgementReclaimsOnlyTheCoveredPrefix(){
        var root=Root();await using(var journal=new TransactionJournal(root,Options())){for(var i=0;i<32;i++){await Append(journal);}journal.AcknowledgeMetadataThrough(12);await journal.CheckpointAndPruneAsync(32);}for(var epoch=0;epoch<4;epoch++){await using var journal=new TransactionJournal(root,Options());await journal.CheckpointAndPruneAsync(32);Assert.Equal(12,journal.MetadataAcknowledgedLsn);Assert.True(journal.Device.FirstAvailableLsn<=13);Assert.Equal(Enumerable.Range(13,20).Select(static value=>(long)value),journal.ReadTransactions().Where(static record=>record.Lsn>12).Select(static record=>record.Lsn));if(epoch==3){Assert.True(journal.Device.FirstAvailableLsn>1);}}
    }
    [Fact]public async Task DevicePruningCannotBypassTheMetadataBoundary(){
        var root=Root();await using(var journal=new TransactionJournal(root,Options())){for(var i=0;i<32;i++){await Append(journal);}await journal.CheckpointAndPruneAsync(32);}for(var epoch=0;epoch<4;epoch++){await using var journal=new TransactionJournal(root,Options());journal.Device.PruneThrough(32);Assert.Equal(1,journal.Device.FirstAvailableLsn);Assert.Equal(32,journal.ReadTransactions().Count());}
    }
    [Theory][InlineData(true)][InlineData(false)]public async Task ChangesAndMetadataCommitAsOneWalRecord(bool commit){
        await using var journal=new TransactionJournal(Root(),Options());await using var coordinator=new TransactionCoordinator(journal);var applied=false;await using(var scope=await coordinator.BeginAsync()){scope.Enlist(new Changes(()=>{Assert.True(journal.Writer.DurableLsn>0);applied=true;}));if(commit){await scope.CommitAsync(new byte[]{7,8,9});}else{await scope.RollbackAsync();}}Assert.Equal(commit,applied);var records=journal.ReadTransactions().ToArray();if(commit){var record=Assert.Single(records);Assert.Single(record.Draft.Entries);Assert.Equal(new byte[]{7,8,9},record.Draft.Metadata);}else{Assert.Empty(records);}
    }
    private sealed class Changes(Action apply):ITransactionChanges{
        public IReadOnlyList<WalEntry> Encode()=>[new(0,1,WalMutation.Upsert,"test-v1",[1],[2])];
        public void Apply()=>apply();
        public void Dispose(){}
    }
    [Theory][InlineData("Written",0L)][InlineData("Published",1L)]public async Task ProcessExitDuringPositionPublicationRecoversThePublishedBoundary(string phase,long acknowledged){
        var root=Root();var start=new System.Diagnostics.ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in new[]{typeof(ProcessEntryPoint).Assembly.Location,"metadata-ack-stop",root,phase}){start.ArgumentList.Add(argument);}using var child=System.Diagnostics.Process.Start(start)!;var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{if(!child.HasExited){child.Kill(entireProcessTree:true);}throw;}Assert.True(child.ExitCode==137,(await output)+(await error));var receipt=System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(await output);Assert.Equal(child.Id,receipt.GetProperty("Pid").GetInt32());Assert.Equal(child.StartTime.ToUniversalTime(),receipt.GetProperty("CreationUtc").GetDateTime());if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){using var current=System.Diagnostics.Process.GetCurrentProcess();Assert.Equal(current.ProcessorAffinity.ToInt64(),receipt.GetProperty("Affinity").GetInt64());}
        await using var journal=new TransactionJournal(root,Options());Assert.Equal(acknowledged,journal.MetadataAcknowledgedLsn);Assert.Single(journal.ReadTransactions());
    }
    internal static int RunStopProbe(string root,string phase){
        var journal=new TransactionJournal(root,Options());Append(journal).GetAwaiter().GetResult();journal.MetadataCheckpoint=current=>{if(current!=phase){return;}using var process=System.Diagnostics.Process.GetCurrentProcess();long? affinity=null;if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){affinity=process.ProcessorAffinity.ToInt64();}Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Pid=process.Id,CreationUtc=process.StartTime.ToUniversalTime(),Affinity=affinity}));Console.Out.Flush();Environment.Exit(137);};journal.AcknowledgeMetadataThrough(1);return 3;
    }
}
