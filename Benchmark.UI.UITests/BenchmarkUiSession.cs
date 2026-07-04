using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Support.UI;

namespace Benchmark.UI.UITests;

internal sealed class BenchmarkUiSession:IDisposable{
    private readonly WindowsDriver driver;

    public BenchmarkUiSession(){
        var appPath=Environment.GetEnvironmentVariable("BENCHMARK_UI_APP_PATH");
        if(string.IsNullOrWhiteSpace(appPath)){
            throw new InvalidOperationException("BENCHMARK_UI_APP_PATH is not set.");
        }

        var serverUrl=Environment.GetEnvironmentVariable("BENCHMARK_UI_APPIUM_URL");
        if(string.IsNullOrWhiteSpace(serverUrl)){
            serverUrl="http://127.0.0.1:4723";
        }

        var options=new AppiumOptions{
            PlatformName="Windows",
            AutomationName="Windows",
        };
        options.App=appPath;
        options.DeviceName="WindowsPC";
        this.driver=new WindowsDriver(new Uri(serverUrl),options,TimeSpan.FromSeconds(60));
    }

    public IWebElement WaitForAutomationId(string automationId,int seconds = 30){
        return this.WaitFor(MobileBy.AccessibilityId(automationId),seconds);
    }

    public void ClickAutomationId(string automationId){
        this.WaitForAutomationId(automationId).Click();
    }

    public bool IsAutomationIdChecked(string automationId){
        return IsChecked(this.WaitForAutomationId(automationId));
    }

    //MAUI の Grid/SfDataGrid/SfTabView は WinUI automation peer を持たず AutomationId が UIA に出ない。
    //SfTabItem は Name=HeaderText(選択中は"Selected "前置)、SfCheckBox は AutomationId で公開されるため、
    //タブは Name、グリッド内要素は AutomationId+Displayed(非選択タブの複製を除外)で特定する。
    public IWebElement WaitForName(string name,int seconds = 30){
        return this.WaitFor(MobileBy.Name(name),seconds);
    }

    public void ClickName(string name){
        this.WaitForName(name).Click();
    }

    public void WaitUntilAutomationIdChecked(string automationId,bool expected,int seconds = 30){
        var wait=new WebDriverWait(this.driver,TimeSpan.FromSeconds(seconds));
        wait.Until(driver => {
            try{
                foreach(var element in driver.FindElements(MobileBy.AccessibilityId(automationId))){
                    if(element.Displayed){
                        return IsChecked(element)==expected;
                    }
                }
                return false;
            }
            catch(WebDriverException){
                return false;
            }
        });
    }

    private IWebElement WaitFor(By locator,int seconds){
        var wait=new WebDriverWait(this.driver,TimeSpan.FromSeconds(seconds));
        return wait.Until(driver => {
            try{
                foreach(var element in driver.FindElements(locator)){
                    if(element.Displayed){
                        return element;
                    }
                }
                return null;
            }
            catch(WebDriverException){
                return null;
            }
        });
    }

    private static bool IsChecked(IWebElement element){
        //SfCheckBox は UIA TogglePattern を公開せず、peer が Name へ "Ticked.../Not Ticked.../Indeterminate..." を返す。
        //Name の先頭で状態を判定する(Indeterminate は未確定として false 扱い)。
        var name=element.Text??string.Empty;
        if(name.StartsWith("Not Ticked",StringComparison.OrdinalIgnoreCase)){
            return false;
        }
        if(name.StartsWith("Ticked",StringComparison.OrdinalIgnoreCase)){
            return true;
        }

        if(element.Selected){
            return true;
        }

        var toggleState=element.GetAttribute("Toggle.ToggleState")??element.GetAttribute("ToggleState");
        return toggleState=="1"||toggleState=="On"||toggleState=="True"||toggleState=="true";
    }

    public void Dispose(){
        this.driver.Quit();
        this.driver.Dispose();
    }
}
