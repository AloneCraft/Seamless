namespace Seamless.Transactions.Tests;

public sealed partial class VersionedTableTests{
    [Fact]
    public async Task OwnedConnectionsKeepTheirIdsAndSnapshotsWithoutChangingTheCallersAmbientRoot(){
        await using var fixture=new Fixture();await fixture.Seed();var id=Guid.NewGuid();await using var first=await fixture.Owner.BeginOwnedAsync(id,false);Assert.False(fixture.Owner.HasCurrentTransaction);fixture.Left.For(first)[1]=(1,250);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.Owner.BeginOwnedAsync(id,false));await using(var second=await fixture.Owner.BeginOwnedAsync(Guid.NewGuid(),true)){Assert.Equal(100,fixture.Left.For(second)[1].Balance);await second.CommitAsync();}
        var before=fixture.Owner.GetDomainDiagnostics().PublishedVersion;var installed=false;var receipt=await first.CommitOwnedAsync(default,context=>{Assert.Equal(id,context.TransactionId);Assert.True(context.CommitVersion>before);Assert.Equal(before,fixture.Owner.GetDomainDiagnostics().PublishedVersion);Assert.Equal(context.CommitVersion,fixture.Left.RecoveredVersion);installed=true;});Assert.True(installed);Assert.Equal(id,receipt.Id);Assert.False(fixture.Owner.HasCurrentTransaction);
        await using var verify=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(250,fixture.Left.For(verify)[1].Balance);await verify.CommitAsync();
    }
}
