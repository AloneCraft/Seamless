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
        var wait=new WebDriverWait(this.driver,TimeSpan.FromSeconds(seconds));
        return wait.Until(driver => {
            try{
                var element=driver.FindElement(MobileBy.AccessibilityId(automationId));
                return element.Displayed ? element : null;
            }
            catch(WebDriverException){
                return null;
            }
        });
    }

    public void ClickAutomationId(string automationId){
        this.WaitForAutomationId(automationId).Click();
    }

    public IWebElement WaitForDescendantAutomationId(string parentAutomationId,string automationId,int seconds = 30){
        var wait=new WebDriverWait(this.driver,TimeSpan.FromSeconds(seconds));
        return wait.Until(driver => {
            try{
                var parent=driver.FindElement(MobileBy.AccessibilityId(parentAutomationId));
                var element=parent.FindElement(MobileBy.AccessibilityId(automationId));
                return element.Displayed ? element : null;
            }
            catch(WebDriverException){
                return null;
            }
        });
    }

    public void ClickDescendantAutomationId(string parentAutomationId,string automationId){
        this.WaitForDescendantAutomationId(parentAutomationId,automationId).Click();
    }

    public bool IsDescendantAutomationIdChecked(string parentAutomationId,string automationId){
        var element=this.WaitForDescendantAutomationId(parentAutomationId,automationId);
        if(element.Selected){
            return true;
        }

        var toggleState=element.GetAttribute("Toggle.ToggleState")??element.GetAttribute("ToggleState");
        return toggleState=="1"||toggleState=="On"||toggleState=="True"||toggleState=="true";
    }

    public bool IsAutomationIdChecked(string automationId){
        var element=this.WaitForAutomationId(automationId);
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
