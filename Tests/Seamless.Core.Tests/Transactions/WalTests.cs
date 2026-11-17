using Seamless.Transactions;
namespace Seamless.Transactions.Tests;
public sealed class WalTests{
    private static WalDraft Draft()=>new(Guid.NewGuid(),[0],[new WalEntry(0,1,WalMutation.Upsert,"account-v1",[1],[2])],[]);
    [Fact]public async Task SyncIsTheCompletionBoundary(){
        using var device=new FakeWalDevice();
        device.BlockSync();
        await using var writer=new WalWriter(device);
        var receipt=await writer.AppendAsync(Draft());
        Assert.True(device.SyncEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(receipt.Durable.IsCompleted);
        Assert.Equal(0,writer.DurableLsn);
        device.ReleaseSync();
        await receipt.Durable.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(receipt.Lsn,writer.DurableLsn);
    }
    [Fact]public async Task SyncFailureFailsEveryWaiterAndFutureAppend(){
        using var device=new FakeWalDevice();
        device.BlockSync();
        await using var writer=new WalWriter(device);
        var first=await writer.AppendAsync(Draft());
        Assert.True(device.SyncEntered.Wait(TimeSpan.FromSeconds(5)));
        var second=await writer.AppendAsync(Draft());
        device.SyncFailure=new IOException("injected sync failure");
        device.ReleaseSync();
        await Assert.ThrowsAsync<IOException>(()=>first.Durable);
        await Assert.ThrowsAsync<IOException>(()=>second.Durable);
        await Assert.ThrowsAsync<IOException>(async()=>await writer.AppendAsync(Draft()));
        Assert.Equal(0,writer.DurableLsn);
    }
    [Fact]public void FrameIsBoundedAndChecksHeaderAndBody(){
        var frame=WalFormat.Encode(new WalTransaction(1,Draft()));
        var decoded=WalFormat.Decode(frame);
        Assert.Equal(1,decoded.Lsn);
        Assert.Equal(new byte[]{2},decoded.Draft.Entries[0].Row);
        frame[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>WalFormat.Decode(frame));
    }
    [Fact]public async Task WriteFailureTerminatesAcceptedAndLaterRequests(){
        using var device=new FakeWalDevice{WriteFailure=new IOException("injected write failure")};await using var writer=new WalWriter(device);
        var receipt=await writer.AppendAsync(Draft());await Assert.ThrowsAsync<IOException>(()=>receipt.Durable);
        await Assert.ThrowsAsync<IOException>(async()=>await writer.AppendAsync(Draft()));Assert.Equal(0,writer.DurableLsn);
    }
}
