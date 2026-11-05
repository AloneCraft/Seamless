using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Seamless.Management.Server;

public static class Program{
    public static async Task<int> Main(string[] args){
        try{
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("初回設定: setup-local --root <新しい保存先> --admin <管理者> --config <起動設定ファイル> / 既存登録: configure-local --root <保存先> --certificate <pfx> --config <起動設定ファイル> / 起動: serve --config <起動設定ファイル> またはIDE用 serve --workspace-config。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("Seamless Management: init --root <directory> --admin <name> / serve --root <directory> --certificate <pfx> [--address 127.0.0.1] [--port 14336] [--ready-file <file>] [--stop-file <file>] / backup --root <stopped root> --destination <empty directory> / restore --backup <directory> --destination <empty directory> / reset-password --root <stopped root> --user <name> --backup <empty directory>. パスワード: SEAMLESS_MANAGEMENT_PASSWORD、証明書: SEAMLESS_MANAGEMENT_CERT_PASSWORD。");return 0;}
            string? Optional(string name){var index=Array.IndexOf(args,name);if(index<0){return null;}if(index+1>=args.Length){throw new ArgumentException(name+"の値が必要です。");}return args[index+1];}
            string Required(string name)=>Optional(name)??throw new ArgumentException(name+"が必要です。");
            if(args[0] is "setup-local" or "configure-local"){
                if(args[0]=="setup-local"){LocalServerSetup.Initialize(Required("--root"),Required("--admin"),ReadPassword(),Required("--config"));}
                else{LocalServerSetup.Register(Required("--root"),Required("--certificate"),Required("--config"),Optional("--local-user"));}
                Console.WriteLine("ローカル起動設定を保存しました。serve --config <設定ファイル>で起動できます。");return 0;
            }
            if(args[0]=="reset-password"){
                var resetRoot=Optional("--root")??Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT")??throw new ArgumentException("--rootが必要です。");
                ResetPassword(resetRoot,Required("--user"),Required("--backup"),ReadPassword());Console.WriteLine("バックアップを作成し、パスワードを再設定しました。権限と有効・無効状態は維持されています。");return 0;
            }
            if(args[0]=="init"){using var initialized=ManagementStore.Initialize(Required("--root"),Required("--admin"),Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_PASSWORD")??throw new ArgumentException("SEAMLESS_MANAGEMENT_PASSWORDが必要です。"));Console.WriteLine("管理領域を初期化しました: "+initialized.InstanceId);return 0;}
            if(args[0] is "backup" or "restore"){
                var source=Optional("--root");if(args[0]=="restore"){source=Required("--backup");}
                CopyOffline(source??throw new ArgumentException("--rootが必要です。"),Required("--destination"));Console.WriteLine("オフラインコピーが完了しました。");return 0;
            }
            if(args[0]!="serve"){throw new ArgumentException("未対応のコマンドです。");}
            var configPath=Optional("--config");if(configPath is null&&Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT") is null&&args.Contains("--workspace-config")){configPath=ManagementLaunchConfiguration.WorkspacePath();}
            ManagementLaunchConfiguration? config=null;if(configPath is not null){config=ManagementLaunchConfiguration.Read(configPath);}
            var root=Optional("--root")??config?.Root??Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT")??throw new ArgumentException("管理領域が未設定です。--root、SEAMLESS_MANAGEMENT_ROOT、または--configを指定してください。");
            var certificatePath=Optional("--certificate")??config?.Certificate??Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_CERTIFICATE")??throw new ArgumentException("サーバー証明書が未設定です。--certificate、SEAMLESS_MANAGEMENT_CERTIFICATE、または--configを指定してください。");
            using var store=new ManagementStore(root);using var service=new ManagementService(store);
            using var certificate=X509CertificateLoader.LoadPkcs12FromFile(certificatePath,Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_CERT_PASSWORD"));
            var port=config?.Port??14336;if(Optional("--port") is{} portText){port=int.Parse(portText,System.Globalization.CultureInfo.InvariantCulture);}
            var address=IPAddress.Parse(Optional("--address")??config?.Address??"127.0.0.1");
            var ownerText=Optional("--owner-pid");var ownerStart=Optional("--owner-start");
            bool OwnerAlive(){if(ownerText is null){return true;}try{using var owner=System.Diagnostics.Process.GetProcessById(int.Parse(ownerText,System.Globalization.CultureInfo.InvariantCulture));return !owner.HasExited&&owner.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)==ownerStart;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}}
            string? localUser=null;if(args.Contains("--local-ipc")){localUser=Optional("--local-user")??config?.LocalUser??"admin";}
            await using var server=new ManagementServer(service,certificate,new IPEndPoint(address,port),localUser);server.Start();
            LocalServerConnection? local=null;if(IPAddress.IsLoopback(address)){local=LocalServerConnection.Publish(store.Root,store.InstanceId,server.Port,certificate,server.PipeName);}
            using var stop=new CancellationTokenSource();ConsoleCancelEventHandler cancel=(_,eventArgs)=>{eventArgs.Cancel=true;stop.Cancel();};Console.CancelKeyPress+=cancel;
            try{
                if(Optional("--ready-file") is{} ready){var process=System.Diagnostics.Process.GetCurrentProcess();File.WriteAllText(ready,JsonSerializer.Serialize(new{store.InstanceId,Port=server.Port,Pid=Environment.ProcessId,CreationUtc=process.StartTime.ToUniversalTime()},ManagementJson.Options));}
                Console.WriteLine("Seamless Management TLS port="+server.Port+" instance="+store.InstanceId);
                while(!stop.IsCancellationRequested){if(!OwnerAlive()||Optional("--stop-file") is{} stopFile&&File.Exists(stopFile)){break;}try{await Task.Delay(250,stop.Token);}catch(OperationCanceledException){break;}}
            }finally{Console.CancelKeyPress-=cancel;local?.Remove(store.Root);}
            return 0;
        }catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
    }
    public static void CopyOffline(string source,string destination){
        using var owner=LockOffline(source);CopyLocked(source,destination);
    }
    private static FileStream LockOffline(string root){
        if(!File.Exists(Path.Combine(root,"management.format"))){throw new InvalidDataException("管理領域ではありません。");}
        try{return new FileStream(Path.Combine(root,"management.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException error){throw new IOException("管理領域を排他取得できません。対象サーバーを停止してから実行してください。",error);}
    }
    public static void ResetPassword(string root,string user,string backup,string password){
        ManagementStore.ValidateAccountName(user);if(password.Length<12){throw new ManagementException("Validation","パスワードは12文字以上にしてください。");}
        using var owner=LockOffline(root);CopyLocked(root,backup);
        using var store=new ManagementStore(root,owner);var key=ManagementStore.Normalize(user);
        var account=store.Read<UserAccount>("user",key)??throw new ManagementException("NotFound","利用者が存在しません。新しい利用者は作成しません。");
        var hash=new Microsoft.AspNetCore.Identity.PasswordHasher<UserAccount>().HashPassword(account,password);
        store.Write(()=>{store.Put("user",key,account with{PasswordHash=hash});store.AddHistory("offline-recovery","*","PasswordReset",account.Name,0);});
    }
    private static string ReadPassword(){
        if(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_PASSWORD") is{} password){return password;}
        if(Console.IsInputRedirected){throw new ArgumentException("非対話実行にはSEAMLESS_MANAGEMENT_PASSWORDが必要です。");}
        string Read(string prompt){Console.Write(prompt);var text=new System.Text.StringBuilder();while(true){var key=Console.ReadKey(intercept:true);if(key.Key==ConsoleKey.Enter){Console.WriteLine();return text.ToString();}if(key.Key==ConsoleKey.Backspace){if(text.Length>0){text.Length--;}continue;}if(key.Key==ConsoleKey.Escape){throw new OperationCanceledException("再設定を取り消しました。");}if(!char.IsControl(key.KeyChar)){text.Append(key.KeyChar);}}}
        var first=Read("新しいパスワード: ");if(first!=Read("確認: ")){throw new ArgumentException("パスワードが一致しません。");}return first;
    }
    private static void CopyLocked(string source,string destination){
        source=Path.GetFullPath(source);destination=Path.GetFullPath(destination);
        if(!File.Exists(Path.Combine(source,"management.format"))){throw new InvalidDataException("管理領域ではありません。");}
        if(destination.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||source.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||string.Equals(source,destination,StringComparison.OrdinalIgnoreCase)){throw new ArgumentException("コピー元と先は独立した領域にしてください。");}
        if(Directory.Exists(destination)&&Directory.EnumerateFileSystemEntries(destination).Any()){throw new IOException("コピー先は空の専用領域にしてください。");}
        Directory.CreateDirectory(destination);var directories=new Queue<string>();directories.Enqueue(source);
        while(directories.TryDequeue(out var current)){foreach(var entry in Directory.EnumerateFileSystemEntries(current)){
            var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む管理領域は明示的に解決してください。");}
            var relative=Path.GetRelativePath(source,entry);var target=Path.Combine(destination,relative);
            if((attributes&FileAttributes.Directory)!=0){Directory.CreateDirectory(target);directories.Enqueue(entry);}else if(relative!="management.lock"){File.Copy(entry,target,overwrite:false);}
        }}
    }
}
