namespace Benchmark;

public partial class App:Application {
    public App() {
        try{
            StartupDiagnostics.Write("App constructor started.");
            InitializeComponent();
            MainPage=new AppShell();
            StartupDiagnostics.Write("App constructor completed.");
        }catch(Exception ex){
            StartupDiagnostics.Write("App constructor failed.",ex);
            MainPage=StartupDiagnostics.CreateErrorPage("Benchmark startup failed",ex);
        }
    }
}
