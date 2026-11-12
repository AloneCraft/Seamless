using System.Diagnostics;
using System.Text.Json;

namespace Seamless.Management.Server;

/// <summary>ネイティブTCPの待受け場所に依存しない、同一OS所有者のブラウザー起動入口。</summary>
public sealed record LocalManagerConnection(Guid InstanceId,string PipeName,int Pid,DateTime CreationUtc){
    public string? ProcessIdentity{get;init;}
    public string? UnixPipePath{get;init;}
    public string GetPipeEndpoint()=>LocalServerConnection.ResolvePipeEndpoint(this.PipeName,this.UnixPipePath)!;
    public const string FileName="manager-connection.json";
    public static LocalManagerConnection? Read(string root){
        var path=Path.Combine(root,FileName);
        if(!File.Exists(path)){return null;}
        var connection=JsonSerializer.Deserialize<LocalManagerConnection>(File.ReadAllText(path),ManagementJson.Options)??throw new InvalidDataException("Managerの接続情報が空です。");
        if(connection.InstanceId==Guid.Empty||connection.PipeName!=LocalServerConnection.PipeNameFor(connection.InstanceId)){throw new InvalidDataException("ManagerのIPC接続情報が不正です。");}
        _=connection.GetPipeEndpoint();
        if(!LocalProcessIdentity.Matches(connection.Pid,connection.CreationUtc,connection.ProcessIdentity)){return null;}
        return connection;
    }
    public static LocalManagerConnection Publish(string root,Guid instanceId,string pipeName){
        string? unixPipePath=null;
        if(OperatingSystem.IsLinux()){unixPipePath=LocalServerConnection.PipeEndpointFor(pipeName);}
        return Publish(root,instanceId,pipeName,unixPipePath);
    }
    public static LocalManagerConnection Publish(string root,Guid instanceId,string pipeName,string? unixPipePath){
        using var process=Process.GetCurrentProcess();
        var createdUtc=process.StartTime.ToUniversalTime();
        var connection=new LocalManagerConnection(instanceId,pipeName,process.Id,createdUtc){ProcessIdentity=LocalProcessIdentity.Capture(process.Id,createdUtc),UnixPipePath=unixPipePath};
        _=connection.GetPipeEndpoint();
        var path=Path.Combine(root,FileName);
        var temporary=path+"."+Guid.NewGuid().ToString("N");
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(connection,ManagementJson.Options));File.Move(temporary,path,true);}
        finally{if(File.Exists(temporary)){File.Delete(temporary);}}
        return connection;
    }
    public void Remove(string root){
        var path=Path.Combine(root,FileName);
        if(File.Exists(path)&&JsonSerializer.Deserialize<LocalManagerConnection>(File.ReadAllText(path),ManagementJson.Options)==this){File.Delete(path);}
    }
}
