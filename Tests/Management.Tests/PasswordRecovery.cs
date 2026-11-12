using Seamless.Management;
using Seamless.Management.Server;
using ServerProgram=Seamless.Management.Server.Program;

namespace Management.Tests;

public sealed class PasswordRecovery{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
    [Fact]public void ResetPassword(){
        var root=Root();var backup=Root();DatabaseGrant[] grants=[new("*",ManagementPermission.All)];
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
            service.ChangeUser(user,new("disabled","DisabledPassword-123",[new("Native",ManagementPermission.View)],true));
            Assert.Throws<IOException>(()=>ServerProgram.ResetPassword(root,"admin",backup,"Replacement-12345"));Assert.False(Directory.Exists(backup));
        }
        var occupied=Root();Directory.CreateDirectory(occupied);File.WriteAllText(Path.Combine(occupied,"existing"),"preserve");Assert.Throws<IOException>(()=>ServerProgram.ResetPassword(root,"admin",occupied,"Replacement-12345"));
        using(var store=new ManagementStore(root))using(var service=new Seamless.Management.Server.ManagementService(store)){Assert.NotNull(service.Login(new("admin","TestPassword-123")));}
        ServerProgram.ResetPassword(root,"admin",backup,"Replacement-12345");
        using(var store=new ManagementStore(root))using(var service=new Seamless.Management.Server.ManagementService(store)){
            Assert.Throws<ManagementException>(()=>service.Login(new("admin","TestPassword-123")));var user=service.Authenticate(service.Login(new("admin","Replacement-12345")).Token);Assert.Equal(grants,user.Grants);Assert.Single(service.Definitions(user).Items);Assert.Contains(service.History(user).Items,item=>item.Action=="PasswordReset");
        }
        using(var store=new ManagementStore(backup))using(var service=new Seamless.Management.Server.ManagementService(store)){var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);Assert.Single(service.Definitions(user).Items);}
        ServerProgram.ResetPassword(root,"disabled",Root(),"ChangedDisabled-123");
        using(var store=new ManagementStore(root)){var disabled=store.Read<UserAccount>("user","DISABLED")!;Assert.True(disabled.Disabled);Assert.Equal(new[]{new DatabaseGrant("Native",ManagementPermission.View)},disabled.Grants);}
        Assert.Equal("NotFound",Assert.Throws<ManagementException>(()=>ServerProgram.ResetPassword(root,"missing",Root(),"Replacement-12345")).Code);
    }
}
