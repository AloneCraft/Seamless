using Seamless.Database.Client;

namespace Management.Tests;

public sealed partial class NativeTls{
    [Theory][InlineData(false)][InlineData(true)]
    public Task NativeAdmissionBoundsMembershipChecksBeforeNewOperations(bool transaction)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var time=new AdmissionClock();using var host=new AuthenticatedFixture(certificate,issuer,time:time,limits:new(){MaximumConcurrentRequests=1});
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await using var first=host.Client();await using var second=host.Client();
        await first.ConnectAsync(timeout.Token);await second.ConnectAsync(timeout.Token);var before=issuer.MembershipChecks;
        var gate=HoldOneMembershipCheck(issuer);time.Advance();var pending=Read(first,timeout.Token);
        try{
            await gate.Entered.WaitAsync(timeout.Token);
            var busy=await Assert.ThrowsAsync<NativeQueryException>(async()=>{
                if(transaction){await using var unexpected=await second.BeginTransactionAsync("fixture",timeout.Token);}
                else{_=await Read(second,timeout.Token);}
            });
            Assert.True(busy.IsRecoverable);Assert.Contains("request limit",busy.Message,StringComparison.Ordinal);Assert.Equal(before+1,issuer.MembershipChecks);Assert.Equal(0,host.Fixture.Module.Calls);
        }finally{gate.Release.TrySetResult();Assert.Equal(8,await pending);}
        Assert.Equal(8,await Read(first,timeout.Token));
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task NativeAdmissionPreservesExistingTransactionCompletion(bool commit)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var time=new AdmissionClock();using var host=new AuthenticatedFixture(certificate,issuer,time:time,limits:new(){MaximumConcurrentRequests=1});
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await using var reader=host.Client();await using var writer=host.Client();await reader.ConnectAsync(timeout.Token);
        await using var transaction=await writer.BeginTransactionAsync("fixture",timeout.Token);Assert.Equal(1,await Scalar(transaction,"insert",7,timeout.Token));var before=issuer.MembershipChecks;
        var gate=HoldOneMembershipCheck(issuer);time.Advance();var pending=Read(reader,timeout.Token);
        try{
            await gate.Entered.WaitAsync(timeout.Token);
            if(commit){await transaction.CommitAsync(timeout.Token);}else{await transaction.RollbackAsync(timeout.Token);}
            Assert.True(transaction.IsCompleted);Assert.Equal(before+1,issuer.MembershipChecks);
        }finally{gate.Release.TrySetResult();Assert.Equal(8,await pending);}
        var expected=0;if(commit){expected=1;}
        Assert.Equal(expected,await reader.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
    });

    [Fact]public Task NativeAdmissionReleasesTheSlotAfterMembershipFailure()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var time=new AdmissionClock();using var host=new AuthenticatedFixture(certificate,issuer,time:time,limits:new(){MaximumConcurrentRequests=1},maximumVerifications:1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await using var first=host.Client();await using var second=host.Client();await first.ConnectAsync(timeout.Token);await second.ConnectAsync(timeout.Token);
        time.Advance();issuer.MembershipUnavailable=true;var error=await Assert.ThrowsAsync<NativeQueryException>(()=>Read(first,timeout.Token));Assert.Contains("membership",error.Message,StringComparison.OrdinalIgnoreCase);Assert.Equal(0,host.Fixture.Module.Calls);
        issuer.MembershipUnavailable=false;Assert.Equal(8,await Read(second,timeout.Token));
    });

    [Fact]public Task NativeAdmissionReleasesTheSlotWhenStoppingARefresh()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var time=new AdmissionClock();using var host=new AuthenticatedFixture(certificate,issuer,time:time,limits:new(){MaximumConcurrentRequests=1},maximumVerifications:1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await using var first=host.Client();await first.ConnectAsync(timeout.Token);
        var gate=HoldOneMembershipCheck(issuer);time.Advance();var pending=Read(first,timeout.Token);
        try{await gate.Entered.WaitAsync(timeout.Token);await host.Fixture.Server.StopAsync().WaitAsync(timeout.Token);await Assert.ThrowsAnyAsync<Exception>(()=>pending);}
        finally{gate.Release.TrySetResult();}
        Assert.Equal(0,host.Fixture.Module.Calls);host.Fixture.Server.Start();await using var second=host.Client();Assert.Equal(8,await Read(second,timeout.Token));
    });

    private static Task<int> Read(NativeQueryClient client,CancellationToken token)=>client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,token);
    private static (Task Entered,TaskCompletionSource Release) HoldOneMembershipCheck(OidcDevelopmentIssuer issuer){
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var calls=0;
        issuer.MembershipHook=token=>{if(Interlocked.Increment(ref calls)!=1){return Task.CompletedTask;}entered.TrySetResult();return release.Task.WaitAsync(token);};
        return(entered.Task,release);
    }
    private sealed class AdmissionClock:TimeProvider{
        private long ticks=DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow()=>new(Interlocked.Read(ref this.ticks),TimeSpan.Zero);
        public void Advance()=>Interlocked.Add(ref this.ticks,TimeSpan.FromMinutes(6).Ticks);
    }
}
