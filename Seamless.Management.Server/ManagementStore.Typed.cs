using System.Text.Json;

namespace Seamless.Management.Server;

public sealed partial class ManagementStore{
    private bool TypedKind(string kind)=>this.Format>=7&&kind is "user" or "serverSettings";
    private ManagementTypedDatabase TypedDatabase=>this.database as ManagementTypedDatabase??throw new InvalidOperationException("型付き管理領域を開いていません。");
    private T? ReadTyped<T>(string kind,string subject){
        var key=new ManagementKey(Key(kind,subject));object value;
        if(kind=="user"){
            if(!this.TypedDatabase.Users.TryGetValue(key,out var row)){return default;}
            if(row.Subject!=subject){throw new InvalidDataException("管理キーが衝突しています。");}value=this.TypedAccount(row);
            if(typeof(T)==typeof(UserAccount)){return(T)value;}
        }else{
            if(!this.TypedDatabase.ServerSettings.TryGetValue(key,out var row)){return default;}
            if(row.Subject!=subject){throw new InvalidDataException("管理キーが衝突しています。");}value=row.Settings();
            if(typeof(T)==typeof(StoredServerSettings)){return(T)value;}
        }
        // 公開Read<T>の任意の型へのJSON投影を、型付きの通常読取と併存させる。
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,value.GetType(),ManagementJson.Options),ManagementJson.Options);
    }
    private void PutTyped<T>(string kind,string subject,T value){
        var key=new ManagementKey(Key(kind,subject));bool exists;
        if(kind=="user"){
            var account=value as UserAccount??JsonSerializer.Deserialize<UserAccount>(JsonSerializer.Serialize(value,ManagementJson.Options),ManagementJson.Options)??throw new InvalidDataException("利用者がありません。");
            ValidateTypedUser(subject,account);ValidateUserSize(account);var table=this.TypedDatabase.Users;exists=table.TryGetValue(key,out var previous);
            if(exists&&previous.Subject!=subject){throw new InvalidDataException("管理キーが衝突しています。");}
            this.RegisterCatalogSubject(kind,subject,exists);this.StoreTypedUser(subject,account,CancellationToken.None);
            this.header=this.header with{CatalogRevision=checked(this.CatalogRevision+1)};
        }else{
            var settings=value as StoredServerSettings??JsonSerializer.Deserialize<StoredServerSettings>(JsonSerializer.Serialize(value,ManagementJson.Options),ManagementJson.Options)??throw new InvalidDataException("設定がありません。");
            if(settings.Version<1||settings.Settings is null){throw new InvalidDataException("設定版が不正です。");}_=settings.Settings.Validate();
            var table=this.TypedDatabase.ServerSettings;exists=table.TryGetValue(key,out var previous);
            if(exists&&previous.Subject!=subject){throw new InvalidDataException("管理キーが衝突しています。");}
            this.RegisterCatalogSubject(kind,subject,exists);table[key]=ManagementSettingsRow.From(subject,settings);
        }
    }
    private static void ValidateTypedUser(string subject,UserAccount account){
        ValidateAccountName(account.Name);
        if(subject!=Normalize(account.Name)){throw new InvalidDataException("利用者のキーが不正です。");}
    }
    private void RegisterCatalogSubject(string kind,string subject,bool exists){
        if(!exists&&kind is not "header" and not "history" and not "catalog" and not "catalogHeader"){
            var counter=this.Read<CatalogCounter>("catalogHeader",kind)??new(0);counter=counter with{Sequence=checked(counter.Sequence+1)};this.Put("catalogHeader",kind,counter);this.Put("catalog",kind+":"+counter.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),new CatalogItem(subject));
        }
    }
    internal void ImportManagedTypes(ManagementStore source,CancellationToken cancellation){
        if(source.Format!=6||this.Format!=7){throw new InvalidOperationException("型付き管理領域の移行元・移行先が不正です。");}
        lock(source.gate){
            using var read=source.database.BeginReadSession();
            foreach(var subjects in source.CatalogSubjects("user",cancellation).Chunk(64)){
                this.Write(()=>{foreach(var subject in subjects){cancellation.ThrowIfCancellationRequested();var account=source.Read<UserAccount>("user",subject);if(account is null){continue;}ValidateTypedUser(subject,account);ValidateUserSize(account);if(this.TypedDatabase.Users.TryGetValue(new(Key("user",subject)),out _)){throw new InvalidDataException("利用者の移行キーが重複しています。");}this.StoreTypedUser(subject,account,cancellation);}});
            }
            foreach(var subject in source.CatalogSubjects("serverSettings",cancellation)){
                cancellation.ThrowIfCancellationRequested();var settings=source.Read<StoredServerSettings>("serverSettings",subject);
                if(settings is not null){this.Write(()=>{if(settings.Version<1){throw new InvalidDataException("設定版が不正です。");}_=settings.Settings.Validate();this.TypedDatabase.ServerSettings[new(Key("serverSettings",subject))]=ManagementSettingsRow.From(subject,settings);});}
            }
        }
    }
    private IEnumerable<string> CatalogSubjects(string kind,CancellationToken cancellation=default){
        var last=this.Read<CatalogCounter>("catalogHeader",kind)?.Sequence??0;
        if(last<0){throw new InvalidDataException("管理カタログの件数が不正です。");}
        for(var index=1L;index<=last;index++){cancellation.ThrowIfCancellationRequested();var entry=this.Read<CatalogItem>("catalog",kind+":"+index.ToString(System.Globalization.CultureInfo.InvariantCulture));if(entry is not null){yield return entry.Subject;}if(index==last){break;}}
    }
    internal void VerifyManagedTypes(ManagementStore source,CancellationToken cancellation){
        long users=0,settings=0;
        foreach(var subject in source.CatalogSubjects("user",cancellation)){
            cancellation.ThrowIfCancellationRequested();var before=source.Read<UserAccount>("user",subject);var after=this.Read<UserAccount>("user",subject);
            if(before is null){if(after is not null){throw new InvalidDataException("利用者が増えています。");}continue;}
            if(after is null||before.Name!=after.Name||before.PasswordHash!=after.PasswordHash||before.Disabled!=after.Disabled||before.Grants is null&&after.Grants is not null||before.Grants is not null&&(after.Grants is null||!before.Grants.SequenceEqual(after.Grants))){throw new InvalidDataException("移行前後の利用者が一致しません。");}users++;
        }
        foreach(var subject in source.CatalogSubjects("serverSettings",cancellation)){cancellation.ThrowIfCancellationRequested();var before=source.Read<StoredServerSettings>("serverSettings",subject);if(before!=this.Read<StoredServerSettings>("serverSettings",subject)){throw new InvalidDataException("移行前後の設定が一致しません。");}if(before is not null){settings++;}}
        long sourceUsers=0,sourceSettings=0;
        using(var sourceRead=source.database.BeginReadSession()){
            foreach(var row in source.database.Entries){cancellation.ThrowIfCancellationRequested();if(row.Kind=="user"){sourceUsers++;}else if(row.Kind=="serverSettings"){sourceSettings++;}}
        }
        if(sourceUsers!=users||sourceSettings!=settings){throw new InvalidDataException("移行元の管理行とカタログが一致しません。");}
        using var read=this.database.BeginReadSession();
        if(this.TypedDatabase.Users.LongCount()!=users||this.TypedDatabase.ServerSettings.LongCount()!=settings){throw new InvalidDataException("型付き管理行の件数が一致しません。");}
        foreach(var row in this.database.Entries){cancellation.ThrowIfCancellationRequested();if(this.TypedKind(row.Kind)||row.Kind=="chunk"&&(row.Subject.StartsWith("user\0",StringComparison.Ordinal)||row.Subject.StartsWith("serverSettings\0",StringComparison.Ordinal))){throw new InvalidDataException("移行対象のJSONが残っています。");}}
    }
}
