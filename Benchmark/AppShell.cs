namespace Benchmark;

public sealed class AppShell:Shell {
    public AppShell() {
        Items.Add(new ShellContent {
            Title="Benchmark",
            Content=new MainPage(),
        });
    }
}
