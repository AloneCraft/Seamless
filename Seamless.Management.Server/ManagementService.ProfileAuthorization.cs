namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private void AuthorizeProfile(UserAccount user,string definitionId){lock(this.store.Gate){this.RequireSource(this.CurrentActor(user),this.Published(definitionId));}}
    private async Task<ObjectExecutionResult> ProfileResultAsync(UserAccount user,string definitionId,Task<ObjectExecutionResult> pending){var result=await pending.ConfigureAwait(false);this.AuthorizeProfile(user,definitionId);return result;}
}
