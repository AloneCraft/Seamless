using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Seamless.Management;

/// <summary>明示された同一マシンの管理領域だけを読む。秘密情報は含めない。</summary>
public sealed record LocalServerConnection(Guid InstanceId,string Host,int Port,int Pid,DateTime CreationUtc,string CertificateSha256,string? PipeName=null){
    public static string PipeNameFor(Guid instanceId)=>"seamless-management-"+instanceId.ToString("N");
    public const string FileName="local-connection.json";
    public const string CertificateFileName="local-server.cer";
    public static LocalServerConnection? Read(string root){
        var path=Path.Combine(root,FileName);if(!File.Exists(path)){return null;}
        var info=JsonSerializer.Deserialize<LocalServerConnection>(File.ReadAllText(path),ManagementJson.Options)??throw new InvalidDataException("ローカル接続情報が空です。");
        if(info.PipeName is not null&&info.PipeName!=PipeNameFor(info.InstanceId)){throw new InvalidDataException("IPC接続情報が不正です。");}
        if(info.InstanceId==Guid.Empty||info.Port is <1 or >65535||info.Host!="localhost"&&(!System.Net.IPAddress.TryParse(info.Host,out var address)||!System.Net.IPAddress.IsLoopback(address))){throw new InvalidDataException("ローカル接続情報が不正です。");}
        try{using var process=Process.GetProcessById(info.Pid);if(process.HasExited||process.StartTime.ToUniversalTime()!=info.CreationUtc){return null;}}
        catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
        var bytes=File.ReadAllBytes(Path.Combine(root,CertificateFileName));if(Convert.ToHexString(SHA256.HashData(bytes))!=info.CertificateSha256){throw new InvalidDataException("ローカル接続情報と公開証明書が一致しません。");}
        return info;
    }
    public static LocalServerConnection Publish(string root,Guid instanceId,int port,X509Certificate2 certificate,string? pipeName=null){
        var bytes=certificate.Export(X509ContentType.Cert);using var process=Process.GetCurrentProcess();
        var info=new LocalServerConnection(instanceId,"localhost",port,process.Id,process.StartTime.ToUniversalTime(),Convert.ToHexString(SHA256.HashData(bytes)),pipeName);
        File.WriteAllBytes(Path.Combine(root,CertificateFileName),bytes);
        var temporary=Path.Combine(root,FileName+"."+Guid.NewGuid().ToString("N"));
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(info,ManagementJson.Options));File.Move(temporary,Path.Combine(root,FileName),overwrite:true);}finally{if(File.Exists(temporary)){File.Delete(temporary);}}
        return info;
    }
    public void Remove(string root){
        var path=Path.Combine(root,FileName);if(!File.Exists(path)){return;}
        var current=JsonSerializer.Deserialize<LocalServerConnection>(File.ReadAllText(path),ManagementJson.Options);
        if(current==this){File.Delete(path);}
    }
}
