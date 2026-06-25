namespace Benchmark;

public partial class App:Application {
    private bool closeConfirmed;
    private bool closeInProgress;

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

    public async Task<bool> RequestCloseAsync(){
        if(this.closeConfirmed||this.closeInProgress){
            return this.closeConfirmed;
        }

        var page = GetMainPage();
        if(page is null){
            this.closeConfirmed=true;
            return true;
        }

        this.closeInProgress=true;
        try{
            if(await page.RequestCloseAsync()){
                this.closeConfirmed=true;
                return true;
            }

            return false;
        }finally{
            this.closeInProgress=false;
        }
    }

    public bool IsCloseConfirmed => this.closeConfirmed;

    private static MainPage? GetMainPage(){
        if(Shell.Current?.CurrentPage is MainPage page){
            return page;
        }

        return null;
    }
}
