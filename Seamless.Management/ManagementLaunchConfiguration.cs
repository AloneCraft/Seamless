using System.Text.Json;

namespace Seamless.Management;

/// <summary>ローカル起動用の公開設定。パスワード・秘密鍵の内容は保存しない。</summary>
public sealed record ManagementLaunchConfiguration(string Root,string Certificate,string Address="127.0.0.1",int Port=14336,string? LocalUser=null){
    public const string FileName="seamless-management.local.json";
    public static string WorkspacePath(){
        foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){
            for(var directory=new DirectoryInfo(start);directory is not null;directory=directory.Parent){
                if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))||File.Exists(Path.Combine(directory.FullName,"Seamless.Management.slnx"))){return Path.Combine(directory.FullName,FileName);}
            }
        }
        throw new ManagementException("Configuration","ワークスペースを特定できません。--config <設定ファイル>を指定してください。");
    }
    public static ManagementLaunchConfiguration Read(string path){
        path=Path.GetFullPath(path);if(!File.Exists(path)){throw new ManagementException("SetupRequired","ローカルサーバーが未設定です。管理ツールでローカルサーバーの初期設定を行ってください。設定ファイル: "+path);}
        var config=JsonSerializer.Deserialize<ManagementLaunchConfiguration>(File.ReadAllText(path),ManagementJson.Options)??throw new ManagementException("Configuration","起動設定が空です。");
        if(string.IsNullOrWhiteSpace(config.Root)||string.IsNullOrWhiteSpace(config.Certificate)||!System.Net.IPAddress.TryParse(config.Address,out _)||config.Port is <0 or >65535){throw new ManagementException("Configuration","起動設定の管理領域・証明書・アドレス・ポートを確認してください。");}
        var directory=Path.GetDirectoryName(path)!;return config with{Root=Path.GetFullPath(config.Root,directory),Certificate=Path.GetFullPath(config.Certificate,directory)};
    }
}
