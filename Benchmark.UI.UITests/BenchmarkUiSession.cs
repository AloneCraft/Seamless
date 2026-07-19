using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace Benchmark.UI.UITests;

internal sealed class ベンチマークUIセッション:IDisposable {
    private readonly WindowsDriver ドライバー;

    public ベンチマークUIセッション() {
        var アプリパス = Environment.GetEnvironmentVariable("BENCHMARK_UI_APP_PATH");
        if(string.IsNullOrWhiteSpace(アプリパス)) {
            throw new InvalidOperationException("BENCHMARK_UI_APP_PATH が設定されていません。");
        }

        var サーバーURL = Environment.GetEnvironmentVariable("BENCHMARK_UI_APPIUM_URL");
        if(string.IsNullOrWhiteSpace(サーバーURL)) {
            サーバーURL="http://127.0.0.1:4723";
        }

        var オプション = new AppiumOptions {
            PlatformName="Windows",
            AutomationName="Windows",
        };
        オプション.App=アプリパス;
        オプション.DeviceName="WindowsPC";
        this.ドライバー=new WindowsDriver(new Uri(サーバーURL),オプション,TimeSpan.FromSeconds(60));
    }

    public IWebElement 自動化IDを待機(string 自動化ID,int 秒 = 30) {
        return this.待機(MobileBy.AccessibilityId(自動化ID),秒);
    }

    public void 自動化IDをクリック(string 自動化ID) {
        this.自動化IDを待機(自動化ID).Click();
    }

    public IReadOnlyList<IWebElement> 表示中の自動化IDを待機(string 自動化ID,int 最小件数,int 秒 = 30) {
        var 待機処理 = new WebDriverWait(this.ドライバー,TimeSpan.FromSeconds(秒));
        return 待機処理.Until(ドライバー => {
            try {
                var 値一覧 = ドライバー.FindElements(MobileBy.AccessibilityId(自動化ID)).Where(static 要素 => 要素.Displayed).ToArray();
                return 値一覧.Length>=最小件数 ? 値一覧 : null;
            } catch(WebDriverException) {
                return null;
            }
        });
    }

    public void チェックボックスをクリック(IWebElement チェック) {
        チェック.Click();
    }
    public void 通常セルをクリック(IWebElement 行チェック,string? 修飾キー = null) {
        var 操作 = new Actions(this.ドライバー);
        if(修飾キー is not null) {
            操作.KeyDown(修飾キー);
        }
        操作.MoveToElement(行チェック,80,0).Click();
        if(修飾キー is not null) {
            操作.KeyUp(修飾キー);
        }
        操作.Perform();
    }

    public void CPUセルをクリック(IWebElement チェック,string? 修飾キー = null) {
        var 操作 = new Actions(this.ドライバー);
        if(修飾キー is not null) {
            操作.KeyDown(修飾キー);
        }
        操作.MoveToElement(チェック,18,0).Click();
        if(修飾キー is not null) {
            操作.KeyUp(修飾キー);
        }
        操作.Perform();
    }

    public void キーを送信(string キー) => new Actions(this.ドライバー).SendKeys(キー).Perform();

    public void 自動化IDへキーを送信(string 自動化ID,string キー) {
        this.自動化IDを待機(自動化ID).SendKeys(キー);
    }

    public bool 自動化IDがチェック済みか(string 自動化ID) {
        return 要素がチェック済みか(this.自動化IDを待機(自動化ID));
    }

    public bool チェック済みか(IWebElement 要素) => 要素がチェック済みか(要素);

    //MAUI の Grid/SfDataGrid/SfTabView は WinUI automation peer を持たず AutomationId が UIA に出ない。
    //SfTabItem は Name=HeaderText(選択中は"Selected "前置)、SfCheckBox は AutomationId で公開されるため、
    //タブは Name、グリッド内要素は AutomationId+Displayed(非選択タブの複製を除外)で特定する。
    public IWebElement 名前を待機(string 名前,int 秒 = 30) {
        return this.待機(MobileBy.Name(名前),秒);
    }

    public void 名前をクリック(string 名前) {
        this.名前を待機(名前).Click();
    }

    public void 自動化IDチェック状態を待機(string 自動化ID,bool 期待値か,int 秒 = 30) {
        var 待機処理 = new WebDriverWait(this.ドライバー,TimeSpan.FromSeconds(秒));
        待機処理.Until(ドライバー => {
            try {
                foreach(var 要素 in ドライバー.FindElements(MobileBy.AccessibilityId(自動化ID))) {
                    if(要素.Displayed) {
                        return 要素がチェック済みか(要素)==期待値か;
                    }
                }
                return false;
            } catch(WebDriverException) {
                return false;
            }
        });
    }

    private IWebElement 待機(By 検索条件,int 秒) {
        var 待機処理 = new WebDriverWait(this.ドライバー,TimeSpan.FromSeconds(秒));
        return 待機処理.Until(ドライバー => {
            try {
                foreach(var 要素 in ドライバー.FindElements(検索条件)) {
                    if(要素.Displayed) {
                        return 要素;
                    }
                }
                return null;
            } catch(WebDriverException) {
                return null;
            }
        });
    }

    private static bool 要素がチェック済みか(IWebElement 要素) {
        //SfCheckBox は UIA TogglePattern を公開せず、peer が Name へ "Ticked.../Not Ticked.../Indeterminate..." を返す。
        //Name の先頭で状態を判定する(Indeterminate は未確定として false 扱い)。
        var 名前 = 要素.Text??string.Empty;
        if(名前.StartsWith("Not Ticked",StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        if(名前.StartsWith("Ticked",StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if(要素.Selected) {
            return true;
        }

        var 切替状態 = 要素.GetAttribute("Toggle.ToggleState")??要素.GetAttribute("ToggleState");
        return 切替状態=="1"||切替状態=="On"||切替状態=="True"||切替状態=="true";
    }

    public void Dispose() {
        this.ドライバー.Quit();
        this.ドライバー.Dispose();
    }
}
