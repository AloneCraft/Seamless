using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Seamless.Management.Server;

public sealed partial class ManagementStore{
    private const int TypedTextCharacters=32768;
    private static ManagementKey GrantKey(string subject,int ordinal)=>new(Key("typed-grant",subject+"\0"+ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    private static ManagementKey TextKey(string kind,string subject,int ordinal,int part)=>new(Key("typed-text",kind+"\0"+subject+"\0"+ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\0"+part.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    private static void ValidateText(ManagementTextValue value){
        if(value.Length is <-1 or >MaximumJsonCharacters||value.Parts<0||value.Parts>(MaximumJsonCharacters+TypedTextCharacters-1)/TypedTextCharacters){throw new InvalidDataException("型付き文字列の長さが不正です。");}
        if(value.Parts==0){if(value.Length==-1&&value.Inline is null){return;}if(value.Inline is null||value.Inline.Length!=value.Length||value.Length>TypedTextCharacters){throw new InvalidDataException("型付き文字列の内容が不正です。");}}
        else if(value.Length<=TypedTextCharacters||value.Inline is not null||value.Parts!=(value.Length+TypedTextCharacters-1)/TypedTextCharacters||value.Hash is not{Length:64}){throw new InvalidDataException("型付き文字列の分割情報が不正です。");}
    }
    private ManagementTextValue WriteText(string kind,string subject,int ordinal,string? value,CancellationToken cancellation){
        if(value is null){return new(null,-1,0,"");}if(value.Length>MaximumJsonCharacters){throw new ManagementException("Limit","管理データの容量上限を超えています。");}
        if(value.Length<=TypedTextCharacters){return new(value,value.Length,0,"");}
        var count=(value.Length+TypedTextCharacters-1)/TypedTextCharacters;
        for(var part=0;part<count;part++){
            cancellation.ThrowIfCancellationRequested();var key=TextKey(kind,subject,ordinal,part);
            if(this.TypedDatabase.ManagedTextParts.TryGetValue(key,out var existing)&&(existing.OwnerKind!=kind||existing.Subject!=subject||existing.Ordinal!=ordinal||existing.Part!=part)){throw new InvalidDataException("管理文字列のキーが衝突しています。");}
            this.TypedDatabase.ManagedTextParts[key]=new(key.Id,kind,subject,ordinal,part,value.AsSpan(part*TypedTextCharacters,Math.Min(TypedTextCharacters,value.Length-part*TypedTextCharacters)).ToArray());
        }
        return new(null,value.Length,count,TextHash(value));
    }
    private string? ReadText(string kind,string subject,int ordinal,ManagementTextValue value){
        ValidateText(value);if(value.Parts==0){return value.Inline;}
        using var memory=this.ResourceBudget.ReserveMemory(checked(2L*value.Length+65536));
        var result=string.Create(value.Length,(Owner:this,Kind:kind,Subject:subject,Ordinal:ordinal,Value:value),static(target,state)=>{
            for(var part=0;part<state.Value.Parts;part++){
                if(!state.Owner.TypedDatabase.ManagedTextParts.TryGetValue(TextKey(state.Kind,state.Subject,state.Ordinal,part),out var row)||row.OwnerKind!=state.Kind||row.Subject!=state.Subject||row.Ordinal!=state.Ordinal||row.Part!=part||row.Text is null||row.Text.Length!=Math.Min(TypedTextCharacters,state.Value.Length-part*TypedTextCharacters)){throw new InvalidDataException("管理文字列の分割行がありません。");}row.Text.AsSpan().CopyTo(target[(part*TypedTextCharacters)..]);
            }
        });
        if(TextHash(result)!=value.Hash){throw new InvalidDataException("管理文字列のhashが一致しません。");}return result;
    }
    private void RemoveText(string kind,string subject,int ordinal,ManagementTextValue value,CancellationToken cancellation){
        ValidateText(value);
        for(var part=0;part<value.Parts;part++){
            cancellation.ThrowIfCancellationRequested();var key=TextKey(kind,subject,ordinal,part);
            if(this.TypedDatabase.ManagedTextParts.TryGetValue(key,out var row)&&(row.OwnerKind!=kind||row.Subject!=subject||row.Ordinal!=ordinal||row.Part!=part)){throw new InvalidDataException("管理文字列のキーが衝突しています。");}this.TypedDatabase.ManagedTextParts.Remove(key);
        }
    }
    private static void ValidateGrantCount(int? count){if(count is <0 or >MaximumJsonCharacters/5){throw new InvalidDataException("利用者の権限件数が不正です。");}}
    private UserAccount TypedAccount(ManagementUserRow row){
        ValidateGrantCount(row.GrantCount);DatabaseGrant[]? grants=null;
        using var memory=this.ResourceBudget.ReserveMemory(checked((long)(row.GrantCount??0)*IntPtr.Size));
        if(row.GrantCount is{} count){
            grants=new DatabaseGrant[count];
            for(var index=0;index<count;index++){
                if(!this.TypedDatabase.UserGrants.TryGetValue(GrantKey(row.Subject,index),out var grant)||grant.Subject!=row.Subject||grant.Ordinal!=index){throw new InvalidDataException("利用者の権限行がありません。");}
                if(!grant.IsNull){memory.Resize(checked(memory.Bytes+32));grants[index]=new(this.ReadText("grant",row.Subject,index,grant.Database)!,grant.Permissions);}
            }
        }
        return new(row.Name,this.ReadText("password",row.Subject,0,row.PasswordHash)!,grants!,row.Disabled);
    }
    private void StoreTypedUser(string subject,UserAccount account,CancellationToken cancellation){
        var key=new ManagementKey(Key("user",subject));
        if(this.TypedDatabase.Users.TryGetValue(key,out var previous)){
            if(previous.Subject!=subject){throw new InvalidDataException("管理キーが衝突しています。");}ValidateGrantCount(previous.GrantCount);this.RemoveText("password",subject,0,previous.PasswordHash,cancellation);
            for(var index=0;index<(previous.GrantCount??0);index++){
                cancellation.ThrowIfCancellationRequested();var grantKey=GrantKey(subject,index);
                if(!this.TypedDatabase.UserGrants.TryGetValue(grantKey,out var old)||old.Subject!=subject||old.Ordinal!=index){throw new InvalidDataException("利用者の権限行がありません。");}this.RemoveText("grant",subject,index,old.Database,cancellation);this.TypedDatabase.UserGrants.Remove(grantKey);
            }
        }
        var password=this.WriteText("password",subject,0,account.PasswordHash,cancellation);
        for(var index=0;index<(account.Grants?.Length??0);index++){
            cancellation.ThrowIfCancellationRequested();var value=account.Grants![index];var grantKey=GrantKey(subject,index);
            if(this.TypedDatabase.UserGrants.TryGetValue(grantKey,out var existing)&&(existing.Subject!=subject||existing.Ordinal!=index)){throw new InvalidDataException("利用者の権限キーが衝突しています。");}
            if(value is null){this.TypedDatabase.UserGrants[grantKey]=new(grantKey.Id,subject,index,new(null,-1,0,""),0,true);}
            else{this.TypedDatabase.UserGrants[grantKey]=new(grantKey.Id,subject,index,this.WriteText("grant",subject,index,value.Database,cancellation),value.Permissions,false);}
        }
        this.TypedDatabase.Users[key]=new(key.Id,subject,account.Name,password,account.Grants?.Length,account.Disabled);
    }
    private static void ValidateUserSize(UserAccount account){
        var maximum=128L+6L*(account.Name.Length+(account.PasswordHash?.Length??0));
        if(account.Grants is not null){foreach(var grant in account.Grants){maximum=checked(maximum+128+6L*(grant?.Database?.Length??0));}}
        if(maximum<=MaximumJsonCharacters){return;}
        using var counter=new JsonCharacterCounter();using var writer=new Utf8JsonWriter(counter);JsonSerializer.Serialize(writer,account,ManagementJson.Options);writer.Flush();
    }
    private static string TextHash(string value){
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var encoder=Encoding.UTF8.GetEncoder();var remaining=value.AsSpan();Span<byte> buffer=stackalloc byte[4096];
        while(!remaining.IsEmpty){encoder.Convert(remaining,buffer,true,out var chars,out var bytes,out _);hash.AppendData(buffer[..bytes]);remaining=remaining[chars..];}return Convert.ToHexString(hash.GetHashAndReset());
    }
    private sealed class JsonCharacterCounter:Stream{
        private readonly Decoder decoder=Encoding.UTF8.GetDecoder();private long length;
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>this.length;
        public override long Position{get=>this.length;set=>throw new NotSupportedException();}
        public override void Flush(){}
        public override void Write(byte[] buffer,int offset,int count)=>this.Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer){this.length=checked(this.length+this.decoder.GetCharCount(buffer,false));if(this.length>MaximumJsonCharacters){throw new ManagementException("Limit","管理データの容量上限を超えています。");}}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
