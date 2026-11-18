using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Authentication;
using Seamless.Security;

namespace Seamless.Management.Server;

public static partial class Program{
    public static async Task<int> Main(string[] args){
        try{
            if(args.Length>0&&args[0] is "ide-server" or "ide-standalone"){
                return await RunIdeAsync(args);
            }
            if(args.Length>0&&args[0]=="manager"){
                var launch=await ManagerBrowserLauncher.CreateLaunchAsync(Required("--root"));
                if(Optional("--launch-file") is{} launchFile){
                    await using var output=new FileStream(launchFile,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                    await output.WriteAsync(System.Text.Encoding.UTF8.GetBytes(launch.Url));
                }else{ManagerBrowserLauncher.Open(launch);}
                Console.WriteLine("所有者セッションでManagerを開きました。");return 0;
            }
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("Manager: manager --root <稼働中サーバーの保存先>。serveは同じプロセスでローカルWeb画面を配信します。--manager-port <ポート>で待受けポートを指定できます。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("初回設定: setup-local --root <新しい保存先> --admin <管理者> --config <起動設定ファイル> / 既存登録: configure-local --root <保存先> --certificate <pfx> --config <起動設定ファイル> / 起動: serve --config <起動設定ファイル> またはIDE用 serve --workspace-config。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("既存DBへ更新監査を導入: enable-update-audit --root <停止中の管理領域> --database <DB名> --backup <空の退避先> [--operation-id <GUID>] / 結果照会: get-operation --root <停止中の管理領域> --operation-id <GUID>。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("通常読み取りの集計: 形式5では既定で1分単位・5秒ごとに保存。--read-audit-window-seconds <秒数>と--read-audit-save-seconds <秒数>で変更、--no-read-auditで停止。旧形式の自動更新は行いません。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("分割WALへの明示移行は upgrade-prepared-wal --root <停止中の形式5管理領域> --backup <空の退避先>。中断時の明示取消は rollback-prepared-wal-upgrade --root <管理領域>。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("利用者・設定の型付き行への明示移行は upgrade-managed-types --root <停止中の形式6管理領域> --backup <空の退避先>。中断時の取消は rollback-managed-types-upgrade --root <管理領域>。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("管理RPCの共有受信予算: serve --maximum-buffered-request-bytes <バイト数>。TCP/TLSとローカルIPCに適用します。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("管理RPCの共有送信予算・期限: serve --maximum-pending-write-bytes <バイト数> --response-write-timeout-ms <ミリ秒>。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("共通認可の初期登録: initialize-authorization --root <停止中の管理領域> --backup <空の退避先> --policy <ポリシーJSON> --oidc <認証元JSON>。API用アクセストークンは標準入力から受け取ります。");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("復元の手順とプロンプト: recovery-guide --scenario <password|startup|migration|restore> [--root <管理領域>] [--backup <バックアップ>] [--error <エラー文>] [--open-folder] [--open-console]");}
            if(args.Length==0||args[0]=="--help"){Console.WriteLine("Seamless Management: init --root <directory> --admin <name> / serve --root <directory> --certificate <pfx> [--address 127.0.0.1] [--port 14336] [--ready-file <file>] [--stop-file <file>] / backup --root <stopped root> --destination <empty directory> / restore --backup <directory> --destination <empty directory> / reset-password --root <stopped root> --user <name> --backup <empty directory> / upgrade-audit --root <stopped root> --backup <empty directory> / rollback-audit-upgrade --root <interrupted root>. パスワード: SEAMLESS_MANAGEMENT_PASSWORD、証明書: SEAMLESS_MANAGEMENT_CERT_PASSWORD。");return 0;}
            string? Optional(string name){var index=Array.IndexOf(args,name);if(index<0){return null;}if(index+1>=args.Length){throw new ArgumentException(name+"の値が必要です。");}return args[index+1];}
            string Required(string name)=>Optional(name)??throw new ArgumentException(name+"が必要です。");
            long? OptionalPositiveNumber(string name){
                if(Optional(name) is not{} value){return null;}
                if(!long.TryParse(value,global::System.Globalization.NumberStyles.None,global::System.Globalization.CultureInfo.InvariantCulture,out var number)||number<=0){throw new ArgumentException(name+" には正の整数を指定してください。");}
                return number;
            }
            if(args[0]=="initialize-authorization"){
                var policyRoot=Required("--root");var backup=Required("--backup");var policyPath=Required("--policy");var providerPath=Required("--oidc");
                if(!Console.IsInputRedirected){throw new ArgumentException("API用アクセストークンをリダイレクトした標準入力から渡してください。");}
                var version=await InitializeAuthorizationAsync(policyRoot,backup,policyPath,providerPath,await Console.In.ReadLineAsync());
                Console.WriteLine("共通認可ポリシーの版 "+version+" を確定しました。");return 0;
            }
            if(args[0]=="enable-update-audit"){
                var id=Guid.NewGuid();if(Optional("--operation-id") is{} text){id=Guid.Parse(text);}using var cancellation=new CancellationTokenSource();ConsoleCancelEventHandler commandCancel=(_,eventArgs)=>{eventArgs.Cancel=true;cancellation.Cancel();};Console.CancelKeyPress+=commandCancel;
                try{Console.WriteLine(JsonSerializer.Serialize(new{operationId=id,phase="Requested"},ManagementJson.Options));var result=UpdateAuditUpgrade.Enable(Required("--root"),Required("--database"),Required("--backup"),id,cancellation.Token);Console.WriteLine(JsonSerializer.Serialize(result,ManagementJson.Options));if(result.State==OperationState.Succeeded){return 0;}return 1;}finally{Console.CancelKeyPress-=commandCancel;}
            }
            if(args[0]=="get-operation"){var result=UpdateAuditUpgrade.GetOperation(Required("--root"),Guid.Parse(Required("--operation-id")));Console.WriteLine(JsonSerializer.Serialize(result,ManagementJson.Options));return 0;}
            if(args[0]=="upgrade-audit"){ManagementStoreUpgrade.Upgrade(Required("--root"),Required("--backup"));Console.WriteLine("監査用の型付きテーブルへ更新しました。");return 0;}
            if(args[0]=="rollback-audit-upgrade"){ManagementStoreUpgrade.Rollback(Required("--root"));Console.WriteLine("中断した監査更新を取り消しました。");return 0;}
            if(args[0]=="upgrade-prepared-wal"){
                using var cancellation=new CancellationTokenSource();ConsoleCancelEventHandler commandCancel=(_,eventArgs)=>{eventArgs.Cancel=true;cancellation.Cancel();};Console.CancelKeyPress+=commandCancel;
                try{ManagementStoreUpgrade.UpgradePreparedWal(Required("--root"),Required("--backup"),cancellation.Token);Console.WriteLine("管理領域を分割WALへ更新しました。");return 0;}finally{Console.CancelKeyPress-=commandCancel;}
            }
            if(args[0]=="rollback-prepared-wal-upgrade"){ManagementStoreUpgrade.RollbackPreparedWal(Required("--root"));Console.WriteLine("中断した分割WAL更新を取り消しました。");return 0;}
            if(args[0]=="upgrade-managed-types"){
                using var cancellation=new CancellationTokenSource();ConsoleCancelEventHandler commandCancel=(_,eventArgs)=>{eventArgs.Cancel=true;cancellation.Cancel();};Console.CancelKeyPress+=commandCancel;
                try{ManagementStoreUpgrade.UpgradeManagedTypes(Required("--root"),Required("--backup"),cancellation.Token);Console.WriteLine("利用者と設定を型付き管理行へ更新しました。");return 0;}finally{Console.CancelKeyPress-=commandCancel;}
            }
            if(args[0]=="rollback-managed-types-upgrade"){ManagementStoreUpgrade.RollbackManagedTypes(Required("--root"));Console.WriteLine("中断した型付き管理更新を取り消しました。");return 0;}
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
            if(args[0]=="recovery-guide"){
                var guideRoot=Optional("--root")??Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT");
                var guide=RecoveryGuideCommand.Create(RecoveryGuide.Parse(Required("--scenario")),guideRoot,Optional("--backup"),Optional("--error"));
                RecoveryGuideCommand.Write(Console.Out,guide);
                var folder=RecoveryGuideCommand.Folder(guide,guideRoot);
                if(args.Contains("--open-folder")){if(folder is null){Console.Error.WriteLine("開くフォルダーがありません。--root を指定してください。");}else{RecoveryGuideCommand.OpenFolder(folder);}}
                if(args.Contains("--open-console")){RecoveryGuideCommand.OpenConsole(folder??Directory.GetCurrentDirectory());}
                return 0;
            }
            if(args[0] is "backup" or "restore"){
                var source=Optional("--root");if(args[0]=="restore"){source=Required("--backup");}
                CopyOffline(source??throw new ArgumentException("--rootが必要です。"),Required("--destination"));Console.WriteLine("オフラインコピーが完了しました。");return 0;
            }
            if(args[0]!="serve"){throw new ArgumentException("未対応のコマンドです。");}
            var configPath=Optional("--config");if(configPath is null&&Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT") is null&&args.Contains("--workspace-config")){configPath=ManagementLaunchConfiguration.WorkspacePath();}
            ManagementLaunchConfiguration? config=null;if(configPath is not null){config=ManagementLaunchConfiguration.Read(configPath);}
            var root=Optional("--root")??config?.Root??Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT")??throw new ArgumentException("管理領域が未設定です。--root、SEAMLESS_MANAGEMENT_ROOT、または--configを指定してください。");
            var certificatePath=Optional("--certificate")??config?.Certificate??Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_CERTIFICATE")??throw new ArgumentException("サーバー証明書が未設定です。--certificate、SEAMLESS_MANAGEMENT_CERTIFICATE、または--configを指定してください。");
            using var store=new ManagementStore(root);
            var bootstrap=new ManagementServerSettings{Address=config?.Address??"127.0.0.1",Port=config?.Port??14336,
                MaximumBufferedRequestBytes=config?.MaximumBufferedRequestBytes,MaximumPendingWriteBytes=config?.MaximumPendingWriteBytes,
                ResponseWriteTimeout=config?.ResponseWriteTimeout,ReadAuditEnabled=store.Format>=5};
            var running=ManagementService.SavedServerSettings(store)?.Settings??bootstrap;
            var overrides=new List<string>();
            if(Optional("--address") is{} addressText){running=running with{Address=addressText};overrides.Add(nameof(ManagementServerSettings.Address));}
            if(Optional("--port") is{} portText){running=running with{Port=int.Parse(portText,System.Globalization.CultureInfo.InvariantCulture)};overrides.Add(nameof(ManagementServerSettings.Port));}
            if(Optional("--manager-port") is{} managerPortText){running=running with{ManagerPort=int.Parse(managerPortText,System.Globalization.CultureInfo.InvariantCulture)};overrides.Add(nameof(ManagementServerSettings.ManagerPort));}
            if(Optional("--manager-external-origin") is{} publicOriginText){running=running with{ExternalManagerOrigin=publicOriginText};overrides.Add(nameof(ManagementServerSettings.ExternalManagerOrigin));}
            if(Optional("--manager-external-address") is{} externalAddressText){running=running with{ExternalManagerAddress=externalAddressText};overrides.Add(nameof(ManagementServerSettings.ExternalManagerAddress));}
            if(OptionalPositiveNumber("--maximum-buffered-request-bytes") is{} buffered){running=running with{MaximumBufferedRequestBytes=buffered};overrides.Add(nameof(ManagementServerSettings.MaximumBufferedRequestBytes));}
            if(OptionalPositiveNumber("--maximum-pending-write-bytes") is{} pending){running=running with{MaximumPendingWriteBytes=pending};overrides.Add(nameof(ManagementServerSettings.MaximumPendingWriteBytes));}
            if(OptionalPositiveNumber("--response-write-timeout-ms") is{} milliseconds){
                if(milliseconds>uint.MaxValue-1){throw new ArgumentException("--response-write-timeout-ms が設定範囲外です。");}
                running=running with{ResponseWriteTimeout=TimeSpan.FromMilliseconds(milliseconds)};overrides.Add(nameof(ManagementServerSettings.ResponseWriteTimeout));
            }
            var auditWindow=Optional("--read-audit-window-seconds");var auditInterval=Optional("--read-audit-save-seconds");var noAudit=args.Contains("--no-read-audit");
            if(noAudit&&(auditWindow is not null||auditInterval is not null)){throw new ArgumentException("--no-read-auditと集計期間・保存間隔は同時に指定できません。");}
            if(auditWindow is not null){running=running with{ReadAuditWindowSeconds=int.Parse(auditWindow,System.Globalization.CultureInfo.InvariantCulture)};overrides.Add(nameof(ManagementServerSettings.ReadAuditWindowSeconds));}
            if(auditInterval is not null){running=running with{ReadAuditSaveSeconds=double.Parse(auditInterval,System.Globalization.CultureInfo.InvariantCulture)};overrides.Add(nameof(ManagementServerSettings.ReadAuditSaveSeconds));}
            if(noAudit||auditWindow is not null||auditInterval is not null){running=running with{ReadAuditEnabled=!noAudit};overrides.Add(nameof(ManagementServerSettings.ReadAuditEnabled));}
            running=running.Validate();
            var ownerText=Optional("--owner-pid");var ownerStart=Optional("--owner-start");
            bool OwnerAlive(){if(ownerText is null){return true;}try{using var owner=System.Diagnostics.Process.GetProcessById(int.Parse(ownerText,System.Globalization.CultureInfo.InvariantCulture));return !owner.HasExited&&owner.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)==ownerStart;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}}
            await using var application=await ManagementApplicationHost.StartServerAsync(store,bootstrap,running,overrides.ToArray(),certificatePath,Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_CERT_PASSWORD"),configPath);
            var manager=application.Manager;
            using var stop=new CancellationTokenSource();ConsoleCancelEventHandler cancel=(_,eventArgs)=>{eventArgs.Cancel=true;stop.Cancel();};Console.CancelKeyPress+=cancel;
            try{
                if(Optional("--ready-file") is{} ready){var process=System.Diagnostics.Process.GetCurrentProcess();File.WriteAllText(ready,JsonSerializer.Serialize(new{store.InstanceId,Port=application.TcpPort,Pid=Environment.ProcessId,CreationUtc=process.StartTime.ToUniversalTime(),ManagerOrigin=manager.Origin.AbsoluteUri,ManagerPath=manager.Sessions.PathBase,PipeName=application.PipeName},ManagementJson.Options));}
                Console.WriteLine("Seamless Management TLS port="+application.TcpPort+" instance="+store.InstanceId);
                Console.WriteLine("ローカルManager: manager --root <管理領域> でブラウザーを開けます。");
                if(application.ExternalManager is not null){Console.WriteLine("外部Manager: "+application.ExternalManager.Origin.AbsoluteUri+"manager/");}
                if(args.Contains("--open-manager",StringComparer.Ordinal)){ManagerBrowserLauncher.Open(manager.Sessions.CreateLaunch());}
                while(!stop.IsCancellationRequested){if(!OwnerAlive()||Optional("--stop-file") is{} stopFile&&File.Exists(stopFile)){break;}try{await Task.Delay(250,stop.Token);}catch(OperationCanceledException){break;}}
            }finally{Console.CancelKeyPress-=cancel;}
            return 0;
        }catch(Exception error){Console.Error.WriteLine(error.Message);if(args.Length>0&&args[0]=="serve"){Console.Error.WriteLine(RecoveryGuideCommand.StartupHint(args,error.Message));}return 1;}
    }
    private static async Task<long> InitializeAuthorizationAsync(string root,string backup,string policyPath,string providerPath,string? accessToken){
        if(string.IsNullOrWhiteSpace(accessToken)){throw new ArgumentException("API用アクセストークンが必要です。");}
        var document=JsonSerializer.Deserialize<AuthorizationPolicyDocument>(await File.ReadAllTextAsync(policyPath),ManagementJson.Options)??throw new InvalidDataException("共通認可ポリシーがありません。");
        var policy=document.Restore();
        if(policy.Version!=1){throw new ArgumentException("初期認可ポリシーは版1で指定してください。");}
        var provider=JsonSerializer.Deserialize<OidcProviderSettings>(await File.ReadAllTextAsync(providerPath),ManagementJson.Options)??throw new InvalidDataException("OIDC認証元の設定がありません。");
        using var verifier=new OidcTokenVerifier(provider);
        var identity=await verifier.AuthenticateAsync(accessToken);
        if(!policy.Resolve(identity).Evaluate(policy.ServerId,SecurityPermission.ManageServer).Allowed){throw new AuthorizationException("PermissionDenied","初期ポリシーのサーバー管理権限が必要です。");}
        using var owner=LockOffline(root);
        CopyLocked(root,backup);
        using var store=new ManagementStore(root,owner,leaveOwnerOpen:true);
        using var authorization=StoredAuthorization.Initialize(store,policy,identity,provider);
        return authorization.Policy.Version;
    }
    public static void CopyOffline(string source,string destination){
        using var owner=LockOffline(source);CopyLocked(source,destination);
    }
    internal static FileStream LockOffline(string root){
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
    internal static void CopyLocked(string source,string destination,CancellationToken cancellation=default){
        cancellation.ThrowIfCancellationRequested();
        source=Path.GetFullPath(source);destination=Path.GetFullPath(destination);
        if(!File.Exists(Path.Combine(source,"management.format"))){throw new InvalidDataException("管理領域ではありません。");}
        if(destination.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||source.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||string.Equals(source,destination,StringComparison.OrdinalIgnoreCase)){throw new ArgumentException("コピー元と先は独立した領域にしてください。");}
        if(Directory.Exists(destination)&&Directory.EnumerateFileSystemEntries(destination).Any()){throw new IOException("コピー先は空の専用領域にしてください。");}
        Directory.CreateDirectory(destination);var directories=new Queue<string>();directories.Enqueue(source);
        while(directories.TryDequeue(out var current)){cancellation.ThrowIfCancellationRequested();foreach(var entry in Directory.EnumerateFileSystemEntries(current)){
            cancellation.ThrowIfCancellationRequested();
            var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0){throw new IOException("リンクを含む管理領域は明示的に解決してください。");}
            var relative=Path.GetRelativePath(source,entry);var target=Path.Combine(destination,relative);
            if((attributes&FileAttributes.Directory)!=0){Directory.CreateDirectory(target);directories.Enqueue(entry);}else if(relative!="management.lock"){File.Copy(entry,target,overwrite:false);}
        }}cancellation.ThrowIfCancellationRequested();
    }
}
