namespace Seamless.Management;

public sealed record UserProfile(UserInfo User,string Stamp);
public sealed record UserProfileEdit(ChangeUserRequest Change,string? ExpectedStamp=null,bool CreateOnly=false){
    public UserProfileEdit Freeze(){
        if(this.Change is null||this.Change.Grants is null||this.Change.Grants.Length>1024||this.Change.Grants.Any(static grant=>grant is null)){
            throw new ManagementException("Validation","利用者と1024件以内の権限を指定してください。");
        }
        return this with{Change=this.Change with{Grants=this.Change.Grants.ToArray()}};
    }
}
