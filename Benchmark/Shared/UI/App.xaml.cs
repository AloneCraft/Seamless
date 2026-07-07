namespace UI;

public partial class App:Application {
    private bool closeConfirmed;
    private bool closeInProgress;
    private readonly Page? startupPage;

    public App(AppShell appShell) {
        try{
            StartupDiagnostics.Write("App constructor started.");
            this.InitializeComponent();
            this.startupPage=appShell;
            StartupDiagnostics.Write("App constructor completed.");
        }catch(Exception ex){
            StartupDiagnostics.Write("App constructor failed.",ex);
            this.startupPage=StartupDiagnostics.CreateErrorPage("Benchmark startup failed",ex);
        }
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(this.startupPage??StartupDiagnostics.CreateErrorPage("Benchmark startup failed",new InvalidOperationException("Startup page was not initialized.")));

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
