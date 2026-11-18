using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management.Server;

public static partial class Program{
    private static async Task<int> RunIdeAsync(string[] args){
        var options=new Dictionary<string,string>(StringComparer.Ordinal);
        for(var i=1;i<args.Length;i+=2){
            if(i+1>=args.Length||args[i] is not("--root" or "--stop-file" or "--launch-file")||!options.TryAdd(args[i],args[i+1])){
                throw new ArgumentException("開発用起動の引数を確認してください。");
            }
        }
        if(!options.TryGetValue("--root",out var path)||string.IsNullOrWhiteSpace(path)){
            throw new ArgumentException("--root に開発用データの保存先を指定してください。");
        }
        var root=Path.IsPathFullyQualified(path)?Path.GetFullPath(path):
            Path.GetFullPath(path,Path.GetDirectoryName(ManagementLaunchConfiguration.WorkspacePath())!);
        var standalone=args[0]=="ide-standalone";
        var mode=standalone?ManagementExecutionMode.Embedded:ManagementExecutionMode.Server;
        var defaults=new ManagementServerSettings{Address="127.0.0.1",Port=0,ManagerPort=0};
        var hostOptions=new ManagementHostOptions(root,mode){Defaults=defaults};
        var format=Path.Combine(root,"management.format");
        var create=!File.Exists(format);
        if(!standalone){
            var config=Path.Combine(root,ManagementLaunchConfiguration.FileName);
            if(create){
                LocalServerSetup.Initialize(root,"local-owner",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),config);
            }
            hostOptions=hostOptions with{Certificate=Path.Combine(root,"server.pfx")};
            create=false;
        }
        await using var host=await (create?ManagementApplicationHost.CreateAsync(hostOptions):ManagementApplicationHost.OpenAsync(hostOptions));
        using var stop=new CancellationTokenSource();
        ConsoleCancelEventHandler cancel=(_,eventArgs)=>{eventArgs.Cancel=true;stop.Cancel();};
        Console.CancelKeyPress+=cancel;
        try{
            var launch=host.Manager.Sessions.CreateLaunch();
            if(options.TryGetValue("--launch-file",out var launchFile)){
                await using var output=new FileStream(launchFile,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                await output.WriteAsync(Encoding.UTF8.GetBytes(launch.Url));
            }else{
                ManagerBrowserLauncher.Open(launch);
            }
            Console.WriteLine($"開発用 {mode} を起動しました。保存先: {root}");
            options.TryGetValue("--stop-file",out var stopFile);
            while(!stop.IsCancellationRequested&&(stopFile is null||!File.Exists(stopFile))){
                try{await Task.Delay(250,stop.Token);}
                catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
            }
            return 0;
        }finally{
            Console.CancelKeyPress-=cancel;
        }
    }
}
