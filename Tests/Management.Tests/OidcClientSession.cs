using System.Collections.Concurrent;
using Seamless.Authentication;
using Session=Seamless.Authentication.OidcClientSession;

namespace Management.Tests;

public sealed class OidcClientSession{
    private static OidcCredentialKey Key(OidcDevelopmentIssuer issuer,Guid instance,string subject="dev-user")=>new(instance,issuer.Id,issuer.Authority,OidcDevelopmentIssuer.Audience,OidcDevelopmentIssuer.ClientId,subject);
    [Fact]public async Task NormalClosePreservesCredentialsAndResumeRotatesBeforeUse(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var store=new Store();var instance=Guid.NewGuid();var result=await issuer.SignInAsync();var key=Key(issuer,instance);
        var session=await Session.SignInAsync(issuer.Settings,instance,store,result);Assert.Equal("dev-user",session.Current!.Subject.Value);Assert.False(string.IsNullOrEmpty(await session.GetAccessTokenAsync()));var saved=store.Read(key);Assert.False(string.IsNullOrEmpty(saved));Assert.Equal(0,issuer.MembershipChecks);
        await session.DisposeAsync();Assert.True(store.Read(key)==saved);
        var resumed=await Session.ResumeAsync(issuer.Settings,instance,"dev-user",store);Assert.NotNull(resumed);await using var restored=resumed;Assert.Equal("dev-user",restored.Current!.Subject.Value);Assert.False(string.IsNullOrEmpty(await restored.GetAccessTokenAsync()));Assert.True(store.Read(key)!=saved);Assert.Equal(1,issuer.RefreshCount);
        await restored.SignOutAsync();Assert.Null(restored.Current);Assert.Null(store.Read(key));await Assert.ThrowsAsync<OidcAuthenticationException>(()=>restored.GetAccessTokenAsync().AsTask());Assert.Null(await Session.ResumeAsync(issuer.Settings,instance,"dev-user",store));Assert.Equal(1,issuer.RefreshCount);
    }
    [Fact]public async Task ConcurrentRequestsRenewOnceAndUseTheSignedExpiration(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();issuer.AccessTokenLifetime=TimeSpan.FromMinutes(1);var store=new Store();var instance=Guid.NewGuid();var clock=new Clock();await using var session=await Session.SignInAsync(issuer.Settings,instance,store,await issuer.SignInAsync(),clock);
        Assert.True(session.Current!.ExpiresAt<clock.GetUtcNow().AddMinutes(2));var before=store.Read(Key(issuer,instance));var reads=store.Reads;
        for(var i=0;i<20;i++){await session.GetAccessTokenAsync();}Assert.Equal(reads,store.Reads);Assert.Equal(0,issuer.RefreshCount);
        issuer.AccessTokenLifetime=TimeSpan.FromMinutes(30);clock.Advance(TimeSpan.FromMinutes(2));var tokens=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>session.GetAccessTokenAsync().AsTask()));Assert.True(tokens.All(value=>value==tokens[0]));Assert.Equal(1,issuer.RefreshCount);Assert.True(store.Read(Key(issuer,instance))!=before);
    }
    [Fact]public async Task InvalidGrantDeletesOnlyTheBoundCredentialButOutagesPreserveIt(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var store=new Store();var instance=Guid.NewGuid();await using(var session=await Session.SignInAsync(issuer.Settings,instance,store,await issuer.SignInAsync())){}
        var key=Key(issuer,instance);var previous=store.Read(key);var other=key with{InstanceId=Guid.NewGuid()};store.Save(other,"other-test-credential");issuer.RefreshUnavailable=true;
        Assert.Equal("IdentityProviderUnavailable",(await Assert.ThrowsAsync<OidcAuthenticationException>(()=>Session.ResumeAsync(issuer.Settings,instance,"dev-user",store))).Code);Assert.True(store.Read(key)==previous);
        issuer.RefreshUnavailable=false;issuer.InvalidateRefreshTokens();Assert.Null(await Session.ResumeAsync(issuer.Settings,instance,"dev-user",store));Assert.Null(store.Read(key));Assert.NotNull(store.Read(other));
    }
    [Fact]public async Task ARefreshCannotSwitchSubjectAudienceOrIdentityToken(){
        for(var variant=0;variant<3;variant++){
            await using var issuer=await OidcDevelopmentIssuer.StartAsync();var store=new Store();var instance=Guid.NewGuid();await using(var session=await Session.SignInAsync(issuer.Settings,instance,store,await issuer.SignInAsync())){}
            if(variant==0){issuer.RefreshSubject="another-user";}else if(variant==1){issuer.RefreshAudience="another-api";}else{issuer.RefreshIdentitySubject="another-user";}
            await Assert.ThrowsAsync<OidcAuthenticationException>(()=>Session.ResumeAsync(issuer.Settings,instance,"dev-user",store));Assert.Null(store.Read(Key(issuer,instance)));
        }
    }
    [Fact]public async Task StorageFailureDoesNotExposeARotatedTokenAsASuccessfulSession(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var store=new Store();var instance=Guid.NewGuid();await using(var session=await Session.SignInAsync(issuer.Settings,instance,store,await issuer.SignInAsync())){}
        store.FailSave=true;await Assert.ThrowsAsync<IOException>(()=>Session.ResumeAsync(issuer.Settings,instance,"dev-user",store));Assert.Equal(1,issuer.RefreshCount);
        store.FailSave=false;Assert.Null(await Session.ResumeAsync(issuer.Settings,instance,"dev-user",store));
    }
    [Fact]public async Task WithoutARefreshGrantOnlyTheCurrentSignInIsAvailable(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();issuer.IssueRefreshToken=false;issuer.AccessTokenLifetime=TimeSpan.FromMinutes(1);var store=new Store();var instance=Guid.NewGuid();var clock=new Clock();await using var session=await Session.SignInAsync(issuer.Settings,instance,store,await issuer.SignInAsync(),clock);
        Assert.Null(store.Read(Key(issuer,instance)));Assert.False(string.IsNullOrEmpty(await session.GetAccessTokenAsync()));clock.Advance(TimeSpan.FromMinutes(2));Assert.Equal("SignInRequired",(await Assert.ThrowsAsync<OidcAuthenticationException>(()=>session.GetAccessTokenAsync().AsTask())).Code);Assert.Equal(0,issuer.RefreshCount);
    }
    [Fact]public async Task CancelingOneWaiterDoesNotConsumeTheRefreshGrantTwice(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();issuer.AccessTokenLifetime=TimeSpan.FromMinutes(1);var store=new Store();var clock=new Clock();await using var session=await Session.SignInAsync(issuer.Settings,Guid.NewGuid(),store,await issuer.SignInAsync(),clock);clock.Advance(TimeSpan.FromMinutes(2));issuer.AccessTokenLifetime=TimeSpan.FromMinutes(30);issuer.RefreshGate=new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));using var cancel=new CancellationTokenSource();var first=session.GetAccessTokenAsync(cancel.Token).AsTask();
        try{
            await issuer.RefreshEntered.Task.WaitAsync(timeout.Token);var second=session.GetAccessTokenAsync().AsTask();cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>first);issuer.RefreshGate.TrySetResult();Assert.False(string.IsNullOrEmpty(await second.WaitAsync(timeout.Token)));Assert.Equal(1,issuer.RefreshCount);
        }finally{issuer.RefreshGate.TrySetResult();}
    }
    [Fact]public async Task ASignInResultCannotBeStoredUnderAnotherProviderOrClient(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var store=new Store();var result=await issuer.SignInAsync();
        foreach(var settings in new[]{issuer.Settings with{Id=Guid.NewGuid()},issuer.Settings with{Authority="http://127.0.0.1:1"},issuer.Settings with{Audience="other-api"},issuer.Settings with{ClientId="another-client"}}){Assert.Equal("IdentityProviderMismatch",(await Assert.ThrowsAsync<OidcAuthenticationException>(()=>Session.SignInAsync(settings,Guid.NewGuid(),store,result))).Code);}Assert.Equal(0,store.Reads);Assert.Equal(0,issuer.RefreshCount);
    }
    private sealed class Clock:TimeProvider{
        private DateTimeOffset now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>this.now;
        public void Advance(TimeSpan duration)=>this.now+=duration;
    }
    private sealed class Store:IOidcSessionCredentialStore{
        private readonly ConcurrentDictionary<OidcCredentialKey,string> values=new();private readonly SemaphoreSlim gate=new(1,1);
        public int Reads{get;private set;}
        public bool FailSave{get;set;}
        public string? Read(OidcCredentialKey key){this.Reads++;return this.values.GetValueOrDefault(key);}
        public void Save(OidcCredentialKey key,string value){if(this.FailSave){throw new IOException("Test store is unavailable.");}this.values[key]=value;}
        public void Delete(OidcCredentialKey key)=>this.values.TryRemove(key,out _);
        public async Task<T> WithLockAsync<T>(OidcCredentialKey key,Func<CancellationToken,Task<T>> action,CancellationToken cancellationToken=default){await this.gate.WaitAsync(cancellationToken);try{return await action(cancellationToken);}finally{this.gate.Release();}}
    }
}
