namespace UI;

public sealed class AppShell:Shell {
    public AppShell(MainPage page) {
        StartupDiagnostics.Write("AppShell constructor started.");
        this.Items.Add(new ShellContent {
            Title="Benchmark",
            Content=page,
        });
        StartupDiagnostics.Write("AppShell constructor completed.");
    }
}
