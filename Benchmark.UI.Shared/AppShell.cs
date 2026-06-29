namespace Benchmark;

public sealed class AppShell:Shell {
    public AppShell() {
        StartupDiagnostics.Write("AppShell constructor started.");
        Page page;
        try{
            page=new MainPage();
        }catch(Exception ex){
            StartupDiagnostics.Write("MainPage construction failed.",ex);
            page=StartupDiagnostics.CreateErrorPage("Benchmark page failed",ex);
        }

        this.Items.Add(new ShellContent {
            Title="Benchmark",
            Content=page,
        });
        StartupDiagnostics.Write("AppShell constructor completed.");
    }
}
