using System.Security.Cryptography;

namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    public Page<UserProfile> UserProfiles(UserAccount actor,long cursor=0){
        lock(this.store.Gate){
            actor=this.CurrentActor(actor);Require(actor,"*",ManagementPermission.Manage);
            var page=this.store.List<UserAccount>("user",cursor);
            return new(page.Items.Select(Profile).ToArray(),page.NextCursor,page.HasMore);
        }
    }
    public UserProfile EditUserProfile(UserAccount actor,UserProfileEdit request){
        request=request.Freeze();var change=request.Change;
        ManagementStore.ValidateAccountName(change.Name);
        if(request.CreateOnly&&request.ExpectedStamp is not null||!request.CreateOnly&&request.ExpectedStamp is not{Length:64}){
            throw new ManagementException("Validation","新規作成、または取得時の版を指定してください。");
        }
        lock(this.store.Gate){
            actor=this.CurrentActor(actor);Require(actor,"*",ManagementPermission.Manage);
            var key=ManagementStore.Normalize(change.Name);var current=this.store.Read<UserAccount>("user",key);
            if(request.CreateOnly){if(current is not null){throw new ManagementException("Conflict","同名の利用者が既に存在します。");}}
            else if(current is null||Profile(current).Stamp!=request.ExpectedStamp){throw new ManagementException("Conflict","利用者が変更されています。入力を確認し、最新の状態を読み直してください。");}
            this.ChangeUser(actor,change);
            return Profile(this.store.Read<UserAccount>("user",key)!);
        }
    }
    private static UserProfile Profile(UserAccount account){
        // パスワード本体を含まない、保存済みアカウント全体の変更検出値だけを返す。
        var stamp=Convert.ToHexString(SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(account,ManagementJson.Options)));
        return new(new(account.Name,account.Grants.ToArray(),account.Disabled),stamp);
    }
}
