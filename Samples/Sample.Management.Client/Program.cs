using Seamless.Management;

namespace Sample.Management.Client;

internal static class Program{
    [STAThread]
    private static void Main(string[] args){
        ApplicationConfiguration.Initialize();
        if(args is not ["--root",var path]){
            MessageBox.Show("--root に開発用サーバーの保存先を指定してください。","Seamless Client");
            return;
        }
        var root=Path.IsPathFullyQualified(path)?Path.GetFullPath(path):
            Path.GetFullPath(path,Path.GetDirectoryName(ManagementLaunchConfiguration.WorkspacePath())!);
        using var form=new Form{Text="Seamless Client — 接続テスト",Width=640,Height=440};
        using var lifetime=new CancellationTokenSource();
        var status=new Label{Text="サーバーの起動を待っています",Dock=DockStyle.Top,AutoSize=true};
        var databases=new ListBox{Dock=DockStyle.Fill};
        var refresh=new Button{Text="DB一覧を更新",Dock=DockStyle.Bottom,AutoSize=true};
        form.Controls.Add(databases);
        form.Controls.Add(status);
        form.Controls.Add(refresh);
        form.FormClosing+=(_,_)=>lifetime.Cancel();
        form.Shown+=async (_,_)=>await RefreshAsync();
        refresh.Click+=async (_,_)=>await RefreshAsync();
        Application.Run(form);

        async Task RefreshAsync(){
            refresh.Enabled=false;
            status.Text="サーバーの起動を待っています";
            try{
                LocalServerConnection? connection;
                while((connection=LocalServerConnection.Read(root)) is null){
                    await Task.Delay(200,lifetime.Token);
                }
                status.Text="サーバーに接続しています";
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                var pipe=connection.GetPipeEndpoint()??throw new InvalidOperationException("サーバーのローカルIPCがありません。");
                using var client=ManagementClient.ForLocalPipe(pipe);
                var info=await client.InfoAsync(timeout.Token);
                if(info.InstanceId!=connection.InstanceId){throw new InvalidOperationException("接続したサーバーが起動情報と一致しません。");}
                var catalog=await client.DatabasesAsync(cancellationToken:timeout.Token);
                databases.Items.Clear();
                databases.Items.AddRange(catalog.Items.Select(db=>(object)$"{db.Name}  (v{db.Version})").ToArray());
                status.Text=$"接続済み  PID {connection.Pid}  ポート {connection.Port}";
            }catch(OperationCanceledException) when(lifetime.IsCancellationRequested){
            }catch(OperationCanceledException){
                databases.Items.Clear();
                status.Text="サーバーから30秒以内に応答がありませんでした。";
            }catch(Exception error){
                databases.Items.Clear();
                status.Text="接続できません: "+error.Message;
            }finally{
                if(!form.IsDisposed){refresh.Enabled=true;}
            }
        }
    }
}
