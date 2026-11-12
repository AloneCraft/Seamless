using System.Runtime.Versioning;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Seamless.Authentication;

namespace Management.Tests;

public sealed class WindowsOidcCredentialStore{
    [OidcWindowsFact,SupportedOSPlatform("windows")]public void SaveReplaceReadAndSignOutUseTheOsStore(){
        var key=new OidcCredentialKey(Guid.NewGuid(),Guid.NewGuid(),"https://identity.invalid","seamless-api","manager","test-user");var store=new Seamless.Authentication.WindowsOidcCredentialStore();
        try{
            Assert.Null(store.Read(key));store.Save(key,"development-test-token-1");Assert.Equal("development-test-token-1",new Seamless.Authentication.WindowsOidcCredentialStore().Read(key));
            store.Save(key,"development-test-token-2");Assert.Equal("development-test-token-2",store.Read(key));
            Assert.Equal("CredentialTooLargeOrInvalid",Assert.Throws<OidcAuthenticationException>(()=>store.Save(key,new string('x',2561))).Code);Assert.Equal("development-test-token-2",store.Read(key));
            foreach(var other in new[]{key with{InstanceId=Guid.NewGuid()},key with{AuthorityId=Guid.NewGuid()},key with{Issuer="https://other.invalid"},key with{Audience="other-api"},key with{ClientId="other-client"},key with{Subject="other-user"}}){Assert.Null(store.Read(other));}
            store.Delete(key);Assert.Null(store.Read(key));store.Delete(key);
        }finally{store.Delete(key);}
    }
    [OidcWindowsFact,SupportedOSPlatform("windows")]public async Task SeparateProcessesRotateTheSameSavedSignInWithoutReusingAnOldGrant(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var instance=Guid.NewGuid();var key=new OidcCredentialKey(instance,issuer.Id,issuer.Authority,OidcDevelopmentIssuer.Audience,OidcDevelopmentIssuer.ClientId,"dev-user");var store=new Seamless.Authentication.WindowsOidcCredentialStore();var children=new List<Process>();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var readyCount=0;
        try{
            await using(var session=await Seamless.Authentication.OidcClientSession.SignInAsync(issuer.Settings,instance,store,await issuer.SignInAsync())){}
            async Task Run(){
                var name="seamless-oidc-test-"+Guid.NewGuid().ToString("N");await using var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--oidc-session-probe",Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(key)),name,"3"}){start.ArgumentList.Add(argument);}
                var process=Process.Start(start)!;children.Add(process);await pipe.WaitForConnectionAsync(timeout.Token);using(var reader=new BinaryReader(pipe,System.Text.Encoding.UTF8,true)){Assert.Equal(process.Id,reader.ReadInt32());Assert.Equal(process.StartTime.ToUniversalTime().Ticks,reader.ReadInt64());Assert.Equal(255,reader.ReadInt64());}
                if(Interlocked.Increment(ref readyCount)==2){ready.TrySetResult();}await ready.Task.WaitAsync(timeout.Token);await pipe.WriteAsync(new byte[]{1},timeout.Token);await pipe.FlushAsync(timeout.Token);var success=new byte[1];Assert.Equal(1,await pipe.ReadAsync(success,timeout.Token));Assert.Equal(1,success[0]);await process.WaitForExitAsync(timeout.Token);Assert.Equal(0,process.ExitCode);
            }
            await Task.WhenAll(Run(),Run());Assert.Equal(6,issuer.RefreshCount);await using var restored=await Seamless.Authentication.OidcClientSession.ResumeAsync(issuer.Settings,instance,"dev-user",store);Assert.NotNull(restored);Assert.Equal(7,issuer.RefreshCount);await restored.SignOutAsync();Assert.Null(store.Read(key));
        }finally{
            foreach(var child in children){try{if(!child.HasExited){child.Kill(entireProcessTree:true);await child.WaitForExitAsync();}}finally{child.Dispose();}}store.Delete(key);
        }
    }
    private sealed class OidcWindowsFactAttribute:FactAttribute{
        public OidcWindowsFactAttribute(){if(!OperatingSystem.IsWindows()){this.Skip="Windows Credential Manager requires Windows; this OS adapter was not executed.";}}
    }
}
