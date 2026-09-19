using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Benchmark.Dynamic.Core;
using Verification.Core;
using Verification.Infrastructure;

namespace Verification.UI;

public sealed class MainPage:ContentPage {
    private readonly ObservableCollection<計画行> 計画行一覧=[];
    private readonly ObservableCollection<履歴行> 履歴行一覧=[];
    private readonly ObservableCollection<結果行> 結果行一覧=[];
    private readonly Dictionary<VerificationSuite,Button> モードボタン一覧=[];
    private readonly Entry リポジトリ入力=new(){AutomationId="検証.リポジトリ",HorizontalOptions=LayoutOptions.Fill};
    private readonly Label DB状態=new(){AutomationId="検証.DB状態",VerticalTextAlignment=TextAlignment.Center};
    private readonly Button 初期化ボタン;
    private readonly Picker プロファイル選択=new(){AutomationId="検証.プロファイル",WidthRequest=120};
    private readonly Switch 全対象切替=new(){AutomationId="検証.全対象"};
    private readonly Picker 容量方式選択=new(){AutomationId="検証.容量方式",WidthRequest=180};
    private readonly Entry ベンチマークTab=new(){Text="System.Array",WidthRequest=150};
    private readonly Entry ベンチマーク型=new(){Text="Int32",WidthRequest=100};
    private readonly Entry ベンチマークMethod=new(){Text="Where_predicate",WidthRequest=170};
    private readonly Entry Seed入力=new(){Text="1",WidthRequest=110};
    private readonly Entry Query数入力=new(){Text="1",Keyboard=Keyboard.Numeric,WidthRequest=70};
    private readonly Picker Source選択=new(){WidthRequest=190};
    private readonly Entry Input数入力=new(){Text="256",Keyboard=Keyboard.Numeric,WidthRequest=90};
    private readonly Entry Operator数入力=new(){Text="4",Keyboard=Keyboard.Numeric,WidthRequest=70};
    private readonly Entry Depth入力=new(){Text="4",Keyboard=Keyboard.Numeric,WidthRequest=70};
    private readonly HorizontalStackLayout 変更設定行=new(){Spacing=8,VerticalOptions=LayoutOptions.Center};
    private readonly HorizontalStackLayout 容量設定行=new(){Spacing=8,VerticalOptions=LayoutOptions.Center};
    private readonly HorizontalStackLayout 性能設定行=new(){Spacing=8,VerticalOptions=LayoutOptions.Center};
    private readonly HorizontalStackLayout 動的設定行=new(){Spacing=8,VerticalOptions=LayoutOptions.Center};
    private readonly Button 計画作成ボタン;
    private readonly Button 実行ボタン;
    private readonly Button 中止ボタン;
    private readonly Button 基準設定ボタン;
    private readonly Button 再現ボタン;
    private readonly Button 再開ボタン;
    private readonly Button 縮小ボタン;
    private readonly Label 状態表示=new(){AutomationId="検証.状態",Text="準備中",LineBreakMode=LineBreakMode.TailTruncation};
    private readonly Label 計画概要=new(){Text="計画はまだ作成されていません。",TextColor=Color.FromArgb("#475569")};
    private readonly ProgressBar 進捗表示=new(){AutomationId="検証.進捗",Progress=0};
    private readonly CollectionView 計画一覧;
    private readonly CollectionView 履歴一覧;
    private readonly CollectionView 結果一覧;
    private readonly Editor 詳細表示=new(){AutomationId="検証.詳細",IsReadOnly=true,AutoSize=EditorAutoSizeOption.TextChanges,FontFamily="Consolas"};
    private VerificationSuite 現在モード=VerificationSuite.Changed;
    private VerificationWorkspace? ワークスペース;
    private VerificationPlan? 現在計画;
    private VerificationRun? 選択履歴;
    private CaseResult? 選択結果;
    private CancellationTokenSource? 実行取消;
    private bool 実行中;

    public MainPage() {
        this.Title="Seamless Verification";
        this.BackgroundColor=Color.FromArgb("#F4F7FB");
        this.リポジトリ入力.Text=FindRepositoryRoot();
        this.初期化ボタン=ActionButton("新規検証DBを初期化","検証.初期化",Color.FromArgb("#0F766E"));
        this.計画作成ボタン=ActionButton("計画作成","検証.計画作成",Color.FromArgb("#334155"));
        this.実行ボタン=ActionButton("実行","検証.実行",Color.FromArgb("#2563EB"));
        this.中止ボタン=ActionButton("中止","検証.中止",Color.FromArgb("#B91C1C"));
        this.基準設定ボタン=ActionButton("基準に設定","検証.基準",Color.FromArgb("#0F766E"));
        this.再現ボタン=ActionButton("replay","検証.再現",Color.FromArgb("#475569"));
        this.再開ボタン=ActionButton("resume","検証.再開",Color.FromArgb("#475569"));
        this.縮小ボタン=ActionButton("失敗縮小","検証.縮小",Color.FromArgb("#7C3AED"));
        this.計画一覧=CreatePlanList(this.計画行一覧);
        this.履歴一覧=CreateHistoryList(this.履歴行一覧);
        this.結果一覧=CreateResultList(this.結果行一覧);
        this.Content=this.CreateLayout();
        this.WireEvents();
        this.ApplyMode();
        this.SetRunning(false);
        this.Appearing+=async(_,_)=>await this.LoadWorkspaceAsync();
    }

    private View CreateLayout() {
        var root=new Grid {
            RowDefinitions={
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(240),
            },
            RowSpacing=8,
            Padding=new Thickness(12),
        };
        root.Add(this.CreateHeader(),0,0);
        root.Add(this.CreateRepositoryBar(),0,1);
        root.Add(this.CreateSettings(),0,2);
        root.Add(this.CreateWorkArea(),0,3);
        root.Add(this.CreateDetails(),0,4);
        return root;
    }

    private View CreateHeader() {
        var header=new Grid {
            ColumnDefinitions={new ColumnDefinition(260),new ColumnDefinition(GridLength.Star)},
            Padding=new Thickness(16,10),
            BackgroundColor=Color.FromArgb("#172033"),
        };
        header.Add(new VerticalStackLayout {
            Spacing=0,
            Children={
                new Label{Text="SEAMLESS",TextColor=Color.FromArgb("#93C5FD"),FontSize=11,FontAttributes=FontAttributes.Bold},
                new Label{Text="Verification Console",TextColor=Colors.White,FontSize=22,FontAttributes=FontAttributes.Bold},
            },
        },0,0);
        var modes=new HorizontalStackLayout{Spacing=6,HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.Center};
        this.AddModeButton(modes,VerificationSuite.Changed,"変更後の確認");
        this.AddModeButton(modes,VerificationSuite.Benchmark,"性能比較");
        this.AddModeButton(modes,VerificationSuite.Scalable,"容量・設定探索");
        this.AddModeButton(modes,VerificationSuite.Dynamic,"クエリの正しさ検証");
        header.Add(modes,1,0);
        return header;
    }

    private View CreateRepositoryBar() {
        var load=ActionButton("読み込み","検証.読込",Color.FromArgb("#475569"));
        load.Clicked+=async(_,_)=>await this.LoadWorkspaceAsync();
        var grid=new Grid {
            ColumnDefinitions={new ColumnDefinition(110),new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto),new ColumnDefinition(310),new ColumnDefinition(GridLength.Auto)},
            ColumnSpacing=8,
            Padding=new Thickness(12,8),
        };
        grid.Add(new Label{Text="リポジトリ",FontAttributes=FontAttributes.Bold,VerticalTextAlignment=TextAlignment.Center},0,0);
        grid.Add(this.リポジトリ入力,1,0);
        grid.Add(load,2,0);
        grid.Add(this.DB状態,3,0);
        grid.Add(this.初期化ボタン,4,0);
        return Panel(grid);
    }

    private View CreateSettings() {
        this.プロファイル選択.ItemsSource=new[]{"quick","full"};
        this.プロファイル選択.SelectedIndex=0;
        this.容量方式選択.ItemsSource=new[]{"Scalable（件数拡張）","Sweep（設定探索）"};
        this.容量方式選択.SelectedIndex=0;
        this.Source選択.ItemsSource=Enum.GetValues<SourceTarget>();
        this.Source選択.SelectedItem=SourceTarget.SystemArray;

        this.変更設定行.Children.Add(new Label{Text="全プロジェクト",VerticalTextAlignment=TextAlignment.Center});
        this.変更設定行.Children.Add(this.全対象切替);
        this.変更設定行.Children.Add(new Label{Text="OFFではGit差分・未追跡・共有ソースから影響範囲を選択します。",TextColor=Color.FromArgb("#64748B"),VerticalTextAlignment=TextAlignment.Center});
        this.容量設定行.Children.Add(new Label{Text="探索方式",VerticalTextAlignment=TextAlignment.Center});
        this.容量設定行.Children.Add(this.容量方式選択);
        this.性能設定行.Children.Add(Field("Tab",this.ベンチマークTab));
        this.性能設定行.Children.Add(Field("Type",this.ベンチマーク型));
        this.性能設定行.Children.Add(Field("Method",this.ベンチマークMethod));
        this.動的設定行.Children.Add(Field("Seed",this.Seed入力));
        this.動的設定行.Children.Add(Field("Queries",this.Query数入力));
        this.動的設定行.Children.Add(Field("Source",this.Source選択));
        this.動的設定行.Children.Add(Field("Input",this.Input数入力));
        this.動的設定行.Children.Add(Field("Operators",this.Operator数入力));
        this.動的設定行.Children.Add(Field("Depth",this.Depth入力));

        var first=new HorizontalStackLayout{Spacing=10,VerticalOptions=LayoutOptions.Center};
        first.Children.Add(new Label{Text="検証プロファイル",FontAttributes=FontAttributes.Bold,VerticalTextAlignment=TextAlignment.Center});
        first.Children.Add(this.プロファイル選択);
        first.Children.Add(this.計画作成ボタン);
        first.Children.Add(this.実行ボタン);
        first.Children.Add(this.中止ボタン);
        first.Children.Add(this.進捗表示);
        this.進捗表示.WidthRequest=160;
        first.Children.Add(this.状態表示);

        return Panel(new VerticalStackLayout {
            Spacing=7,
            Children={first,this.変更設定行,this.性能設定行,this.容量設定行,this.動的設定行,this.計画概要},
        });
    }

    private View CreateWorkArea() {
        var grid=new Grid {
            ColumnDefinitions={new ColumnDefinition(new GridLength(1.25,GridUnitType.Star)),new ColumnDefinition(new GridLength(.85,GridUnitType.Star)),new ColumnDefinition(new GridLength(1.15,GridUnitType.Star))},
            ColumnSpacing=8,
        };
        grid.Add(Panel(Section("実行対象と選定理由",this.計画一覧)),0,0);
        grid.Add(Panel(Section("履歴・比較基準",this.CreateHistorySection())),1,0);
        grid.Add(Panel(Section("ケース結果",this.結果一覧)),2,0);
        return grid;
    }

    private View CreateHistorySection() {
        var actions=new HorizontalStackLayout{Spacing=5};
        actions.Children.Add(this.基準設定ボタン);
        actions.Children.Add(this.再現ボタン);
        actions.Children.Add(this.再開ボタン);
        actions.Children.Add(this.縮小ボタン);
        return new Grid {
            RowDefinitions={new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star)},
            Children={actions,this.履歴一覧},
        }.ConfigureRows();
    }

    private View CreateDetails()=>Panel(Section("選択ケースの条件・測定・診断・成果物",new ScrollView{Content=this.詳細表示}));

    private void AddModeButton(HorizontalStackLayout parent,VerificationSuite suite,string text) {
        var button=new Button {
            Text=text,
            AutomationId="検証.モード."+text,
            Padding=new Thickness(13,7),
            CornerRadius=5,
            FontSize=13,
        };
        button.Clicked+=(_,_)=>{this.現在モード=suite;this.ApplyMode();};
        this.モードボタン一覧.Add(suite,button);
        parent.Children.Add(button);
    }

    private void WireEvents() {
        this.初期化ボタン.Clicked+=async(_,_)=>await this.InitializeAsync();
        this.計画作成ボタン.Clicked+=async(_,_)=>await this.CreatePlanAsync();
        this.実行ボタン.Clicked+=async(_,_)=>await this.RunCurrentPlanAsync();
        this.中止ボタン.Clicked+=(_,_)=>this.実行取消?.Cancel();
        this.基準設定ボタン.Clicked+=async(_,_)=>await this.SetBaselineAsync();
        this.再現ボタン.Clicked+=async(_,_)=>await this.ReplayAsync();
        this.再開ボタン.Clicked+=async(_,_)=>await this.ResumeAsync();
        this.縮小ボタン.Clicked+=async(_,_)=>await this.ShrinkAsync();
        this.履歴一覧.SelectionChanged+=(_,eventArgs)=>this.SelectRun(eventArgs.CurrentSelection.FirstOrDefault() as 履歴行);
        this.結果一覧.SelectionChanged+=(_,eventArgs)=>this.SelectResult(eventArgs.CurrentSelection.FirstOrDefault() as 結果行);
    }

    private void ApplyMode() {
        foreach(var pair in this.モードボタン一覧) {
            var active=pair.Key==this.現在モード
                ||this.現在モード==VerificationSuite.Sweep&&pair.Key==VerificationSuite.Scalable;
            pair.Value.BackgroundColor=active?Color.FromArgb("#2563EB"):Color.FromArgb("#334155");
            pair.Value.TextColor=Colors.White;
        }
        this.変更設定行.IsVisible=this.現在モード==VerificationSuite.Changed;
        this.性能設定行.IsVisible=this.現在モード==VerificationSuite.Benchmark;
        this.容量設定行.IsVisible=this.現在モード is VerificationSuite.Scalable or VerificationSuite.Sweep;
        this.動的設定行.IsVisible=this.現在モード==VerificationSuite.Dynamic;
    }

    private async Task LoadWorkspaceAsync() {
        if(this.実行中){return;}
        try {
            var root=Path.GetFullPath(this.リポジトリ入力.Text??string.Empty);
            this.リポジトリ入力.Text=root;
            if(!File.Exists(Path.Combine(root,"Seamless.slnx"))) {
                throw new FileNotFoundException("Seamless.slnxが見つかりません。",Path.Combine(root,"Seamless.slnx"));
            }
            if(!VerificationWorkspace.IsInitialized(root)) {
                this.ワークスペース=null;
                this.DB状態.Text="未初期化: "+VerificationWorkspace.GetDatabasePath(root);
                this.DB状態.TextColor=Color.FromArgb("#B45309");
                this.初期化ボタン.IsVisible=true;
                this.履歴行一覧.Clear();
                this.結果行一覧.Clear();
                this.SetStatus("新規検証DBを明示的に初期化してください。",false);
                return;
            }
            var workspace=await Task.Run(()=>new VerificationWorkspace(root));
            this.ワークスペース=workspace;
            this.DB状態.Text="接続済み: "+workspace.DatabasePath;
            this.DB状態.TextColor=Color.FromArgb("#15803D");
            this.初期化ボタン.IsVisible=false;
            this.RefreshHistory();
            var recovered=workspace.RecoveredInterruptedRuns;
            this.SetStatus(recovered>0?$"中断実行を{recovered}件回収しました。":"履歴を読み込みました。",true);
        } catch(Exception error) {
            this.ワークスペース=null;
            this.SetStatus(error.Message,false);
        }
        this.SetRunning(false);
    }

    private async Task InitializeAsync() {
        try {
            var root=Path.GetFullPath(this.リポジトリ入力.Text??string.Empty);
            await Task.Run(()=>VerificationWorkspace.Initialize(root));
            await this.LoadWorkspaceAsync();
        } catch(Exception error) {
            this.SetStatus(error.Message,false);
        }
    }

    private async Task CreatePlanAsync() {
        if(this.ワークスペース is null){this.SetStatus("検証DBを読み込んでください。",false);return;}
        try {
            this.SetRunning(true);
            this.SetStatus("Git差分と依存関係から検証対象を選択しています…",true);
            var request=this.ReadPlanRequest();
            var plan=await Task.Run(()=>this.ワークスペース.CreatePlan(request));
            this.現在計画=plan;
            this.計画行一覧.Clear();
            foreach(var item in plan.Cases) {
                this.計画行一覧.Add(new(item,$"{item.Name}\n{item.Area}",item.SelectionReason));
            }
            this.計画概要.Text=$"{plan.Profile} / {plan.Cases.Length}ケース / 時間枠: {(plan.BudgetSeconds==0?"制限なし":plan.BudgetSeconds+"秒")} / ソース: {plan.SourceFingerprint[..16]}";
            this.SetStatus(plan.Cases.Length==0?"対象ケースがありません。":"計画を作成しました。",plan.Cases.Length>0);
        } catch(Exception error) {
            this.SetStatus(error.Message,false);
        } finally {
            this.SetRunning(false);
        }
    }

    private VerificationPlanRequest ReadPlanRequest() {
        var suite=this.現在モード;
        if(suite==VerificationSuite.Changed&&this.全対象切替.IsToggled){suite=VerificationSuite.All;}
        if(suite is VerificationSuite.Scalable or VerificationSuite.Sweep) {
            suite=this.容量方式選択.SelectedIndex==1?VerificationSuite.Sweep:VerificationSuite.Scalable;
        }
        return new(
            suite,
            this.プロファイル選択.SelectedItem?.ToString()??"quick",
            this.ベンチマークTab.Text??"System.Array",
            this.ベンチマーク型.Text??"Int32",
            this.ベンチマークMethod.Text??"Where_predicate",
            this.Seed入力.Text??"1",
            ParsePositive(this.Query数入力,"Queries"),
            this.Source選択.SelectedItem is SourceTarget source?source:SourceTarget.SystemArray,
            ParseNonNegative(this.Input数入力,"Input"),
            ParsePositive(this.Operator数入力,"Operators"),
            ParsePositive(this.Depth入力,"Depth"));
    }

    private async Task RunCurrentPlanAsync() {
        if(this.ワークスペース is null||this.現在計画 is null){this.SetStatus("先に検証計画を作成してください。",false);return;}
        try {
            var fingerprint=await Task.Run(()=>RepositoryInspection.Fingerprint(this.ワークスペース.RepositoryRoot));
            if(fingerprint!=this.現在計画.SourceFingerprint) {
                throw new InvalidOperationException("計画作成後にソースが変わりました。計画を作り直してください。");
            }
            await this.RunPlanAsync(this.現在計画,null);
        } catch(Exception error) {
            this.SetStatus(error.Message,false);
            this.SetRunning(false);
        }
    }

    private async Task RunPlanAsync(VerificationPlan plan,string? parentId) {
        if(this.ワークスペース is null){return;}
        this.SetRunning(true);
        this.進捗表示.Progress=0;
        this.実行取消=new();
        var completed=0;
        var progress=new Progress<VerificationProgress>(value=>{
            this.SetStatus(value.CaseId+" / "+value.Status+" / "+value.Message,value.Status!=CaseStatus.Failed);
            if(value.Status is CaseStatus.Passed or CaseStatus.Failed or CaseStatus.Cancelled or CaseStatus.Interrupted){completed++;}
            this.進捗表示.Progress=plan.Cases.Length==0?0:Math.Min(1,(double)completed/plan.Cases.Length);
        });
        try {
            var run=await this.ワークスペース.RunAsync(plan,progress,parentId,this.実行取消.Token);
            this.SetStatus("実行完了: "+run.Id+" / "+run.Status,run.Status==CaseStatus.Passed);
            this.RefreshHistory(run.Id);
        } catch(Exception error) {
            this.SetStatus(error.Message,false);
        } finally {
            this.実行取消.Dispose();
            this.実行取消=null;
            this.SetRunning(false);
        }
    }

    private void RefreshHistory(string? selectRunId=null) {
        if(this.ワークスペース is null){return;}
        var baseline=this.ワークスペース.BaselineId;
        this.履歴行一覧.Clear();
        履歴行? selection=null;
        foreach(var run in this.ワークスペース.GetRuns()) {
            var marker=run.Id==baseline?" ★基準":string.Empty;
            var row=new 履歴行(run,$"{run.StartedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}  {run.Status}{marker}\n{run.ProfileDisplay()} / {run.Plan.Cases.Length}件 / {run.Id[..12]}");
            this.履歴行一覧.Add(row);
            if(run.Id==selectRunId){selection=row;}
        }
        if(selection is not null){this.履歴一覧.SelectedItem=selection;}
    }

    private void SelectRun(履歴行? row) {
        this.選択履歴=row?.Run;
        this.選択結果=null;
        this.結果行一覧.Clear();
        this.詳細表示.Text=string.Empty;
        if(row is null||this.ワークスペース is null){this.UpdateHistoryActions();return;}
        IReadOnlyDictionary<string,ComparisonResult> comparisons=new Dictionary<string,ComparisonResult>();
        if(this.ワークスペース.BaselineId is not null&&this.ワークスペース.BaselineId!=row.Run.Id) {
            comparisons=this.ワークスペース.Compare(row.Run.Id).ToDictionary(static result=>result.CaseId,StringComparer.Ordinal);
        }
        foreach(var result in this.ワークスペース.GetResults(row.Run.Id)) {
            comparisons.TryGetValue(result.CaseId,out var comparison);
            var compareText=comparison is null?string.Empty:" / "+comparison.Status;
            this.結果行一覧.Add(new(result,comparison,$"{StatusMark(result.Status)} {result.CaseId}\n{result.Status}{compareText}"));
        }
        this.UpdateHistoryActions();
    }

    private void SelectResult(結果行? row) {
        this.選択結果=row?.Result;
        this.詳細表示.Text=row is null?string.Empty:FormatDetails(row);
        this.UpdateHistoryActions();
    }

    private async Task SetBaselineAsync() {
        if(this.ワークスペース is null||this.選択履歴 is null){return;}
        try {
            await Task.Run(()=>this.ワークスペース.SetBaseline(this.選択履歴.Id));
            this.RefreshHistory(this.選択履歴.Id);
            this.SetStatus("固定比較基準を更新しました: "+this.選択履歴.Id,true);
        } catch(Exception error){this.SetStatus(error.Message,false);}
    }

    private async Task ReplayAsync() {
        if(this.ワークスペース is null||this.選択履歴 is null){return;}
        try {
            var plan=await Task.Run(()=>this.ワークスペース.CreateReplayPlan(this.選択履歴.Id,this.選択結果?.CaseId));
            await this.RunPlanAsync(plan,this.選択履歴.Id);
        } catch(Exception error){this.SetStatus(error.Message,false);this.SetRunning(false);}
    }

    private async Task ResumeAsync() {
        if(this.ワークスペース is null||this.選択履歴 is null){return;}
        try {
            var plan=await Task.Run(()=>this.ワークスペース.CreateResumePlan(this.選択履歴.Id));
            await this.RunPlanAsync(plan,this.選択履歴.Id);
        } catch(Exception error){this.SetStatus(error.Message,false);this.SetRunning(false);}
    }

    private async Task ShrinkAsync() {
        if(this.ワークスペース is null||this.選択履歴 is null||this.選択結果 is null){return;}
        try {
            var plan=await Task.Run(()=>this.ワークスペース.CreateShrinkPlan(this.選択履歴.Id,this.選択結果.CaseId));
            await this.RunPlanAsync(plan,this.選択履歴.Id);
        } catch(Exception error){this.SetStatus(error.Message,false);this.SetRunning(false);}
    }

    private void UpdateHistoryActions() {
        var run=this.選択履歴;
        var result=this.選択結果;
        this.基準設定ボタン.IsEnabled=!this.実行中&&run?.Status==CaseStatus.Passed;
        this.再現ボタン.IsEnabled=!this.実行中&&run is not null&&(result is not null||run.Status!=CaseStatus.Passed);
        this.再開ボタン.IsEnabled=!this.実行中&&run?.Status is CaseStatus.Cancelled or CaseStatus.Interrupted;
        this.縮小ボタン.IsEnabled=!this.実行中&&run is not null&&result is not null
            &&result.Status==CaseStatus.Failed
            &&run.Plan.Cases.Any(item=>item.Id==result.CaseId&&item.Adapter=="dynamic-worker");
    }

    private void SetRunning(bool value) {
        this.実行中=value;
        this.計画作成ボタン.IsEnabled=!value&&this.ワークスペース is not null;
        this.実行ボタン.IsEnabled=!value&&this.ワークスペース is not null&&this.現在計画?.Cases.Length>0;
        this.中止ボタン.IsEnabled=value;
        this.初期化ボタン.IsEnabled=!value;
        this.UpdateHistoryActions();
    }

    private void SetStatus(string text,bool normal) {
        this.状態表示.Text=text;
        this.状態表示.TextColor=normal?Color.FromArgb("#334155"):Color.FromArgb("#B91C1C");
    }

    private static string FormatDetails(結果行 row) {
        var result=row.Result;
        var builder=new StringBuilder();
        builder.AppendLine("Case: "+result.CaseId);
        builder.AppendLine("Status: "+result.Status);
        if(row.Comparison is not null) {
            builder.AppendLine("Comparison: "+row.Comparison.Status+" / "+row.Comparison.Reason);
            if(row.Comparison.TimeRatio is double ratio){builder.AppendLine("Time ratio: "+ratio.ToString("0.####",CultureInfo.InvariantCulture));}
        }
        builder.AppendLine("Semantics: "+result.Conditions.Semantics);
        builder.AppendLine("Codec: "+result.Conditions.CodecVersion);
        builder.AppendLine("Measurement: "+result.Conditions.MeasurementVersion);
        builder.AppendLine("Configuration: "+result.Conditions.Configuration);
        builder.AppendLine("CPU: "+result.Conditions.Cpu);
        builder.AppendLine("Environment: "+result.Conditions.Environment);
        builder.AppendLine("Input hash: "+result.Conditions.InputHash);
        builder.AppendLine("Build hash: "+(result.BuildFingerprint??"なし"));
        if(result.ElapsedNanoseconds.Length>0) {
            builder.AppendLine("Elapsed samples: "+result.ElapsedNanoseconds.Length);
            builder.AppendLine("Median ns: "+Median(result.ElapsedNanoseconds).ToString("0.###",CultureInfo.InvariantCulture));
        }
        if(result.AllocatedBytes is long allocated){builder.AppendLine("Allocated bytes: "+allocated.ToString("N0",CultureInfo.InvariantCulture));}
        builder.AppendLine("Peak private bytes: "+result.PeakPrivateBytes.ToString("N0",CultureInfo.InvariantCulture));
        if(result.Metrics is not null) {
            foreach(var metric in result.Metrics.OrderBy(static pair=>pair.Key,StringComparer.Ordinal)) {
                builder.AppendLine(metric.Key+": "+metric.Value.ToString("0.####",CultureInfo.InvariantCulture));
            }
        }
        builder.AppendLine();
        builder.AppendLine("Diagnostic:");
        builder.AppendLine(result.Diagnostic??"なし");
        builder.AppendLine();
        builder.AppendLine("Artifacts:");
        foreach(var artifact in result.Artifacts??[]) {
            builder.AppendLine($"- {artifact.RelativePath} ({artifact.Length:N0} bytes, SHA256 {artifact.Sha256})");
        }
        return builder.ToString();
    }

    private static CollectionView CreatePlanList(ObservableCollection<計画行> source)=>new() {
        AutomationId="検証.計画一覧",
        ItemsSource=source,
        SelectionMode=SelectionMode.None,
        ItemTemplate=new DataTemplate(()=>TwoLineTemplate(nameof(計画行.表示),nameof(計画行.補足))),
    };

    private static CollectionView CreateHistoryList(ObservableCollection<履歴行> source)=>new() {
        AutomationId="検証.履歴一覧",
        ItemsSource=source,
        SelectionMode=SelectionMode.Single,
        ItemTemplate=new DataTemplate(()=>OneLineTemplate(nameof(履歴行.表示))),
    };

    private static CollectionView CreateResultList(ObservableCollection<結果行> source)=>new() {
        AutomationId="検証.結果一覧",
        ItemsSource=source,
        SelectionMode=SelectionMode.Single,
        ItemTemplate=new DataTemplate(()=>OneLineTemplate(nameof(結果行.表示))),
    };

    private static View TwoLineTemplate(string titlePath,string detailPath) {
        var title=new Label{FontAttributes=FontAttributes.Bold,FontSize=13,LineBreakMode=LineBreakMode.WordWrap};
        title.SetBinding(Label.TextProperty,titlePath);
        var detail=new Label{FontSize=12,TextColor=Color.FromArgb("#64748B"),LineBreakMode=LineBreakMode.WordWrap};
        detail.SetBinding(Label.TextProperty,detailPath);
        return new Border{Stroke=Color.FromArgb("#E2E8F0"),StrokeThickness=0,Padding=new Thickness(7),Content=new VerticalStackLayout{Spacing=2,Children={title,detail}}};
    }

    private static View OneLineTemplate(string path) {
        var label=new Label{FontSize=12,LineBreakMode=LineBreakMode.WordWrap};
        label.SetBinding(Label.TextProperty,path);
        return new Border{Stroke=Color.FromArgb("#E2E8F0"),StrokeThickness=0,Padding=new Thickness(7),Content=label};
    }

    private static View Section(string title,View content)=>new Grid {
        RowDefinitions={new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star)},
        RowSpacing=6,
        Children={new Label{Text=title,FontAttributes=FontAttributes.Bold,FontSize=15},content},
    }.ConfigureRows();

    private static Border Panel(View content)=>new() {
        BackgroundColor=Colors.White,
        Stroke=Color.FromArgb("#CBD5E1"),
        StrokeThickness=1,
        Padding=new Thickness(10),
        Content=content,
    };

    private static Button ActionButton(string text,string automationId,Color color)=>new() {
        Text=text,
        AutomationId=automationId,
        BackgroundColor=color,
        TextColor=Colors.White,
        Padding=new Thickness(11,6),
        CornerRadius=5,
        FontSize=12,
    };

    private static View Field(string label,View control)=>new HorizontalStackLayout {
        Spacing=4,
        VerticalOptions=LayoutOptions.Center,
        Children={new Label{Text=label,VerticalTextAlignment=TextAlignment.Center},control},
    };

    private static int ParsePositive(Entry entry,string name) {
        var value=ParseNonNegative(entry,name);
        if(value==0){throw new ArgumentOutOfRangeException(name,name+"は1以上にしてください。");}
        return value;
    }

    private static int ParseNonNegative(Entry entry,string name) {
        if(!int.TryParse(entry.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var value)||value<0) {
            throw new ArgumentOutOfRangeException(name,name+"は0以上の整数にしてください。");
        }
        return value;
    }

    private static double Median(double[] values) {
        var copy=(double[])values.Clone();
        Array.Sort(copy);
        var middle=copy.Length/2;
        if(copy.Length%2==0){return copy[middle-1]/2+copy[middle]/2;}
        return copy[middle];
    }

    private static string StatusMark(CaseStatus status)=>status switch {
        CaseStatus.Passed=>"●",
        CaseStatus.Failed=>"×",
        CaseStatus.Running=>"▶",
        CaseStatus.Cancelled=>"■",
        CaseStatus.Interrupted=>"!",
        _=>"○",
    };

    private static string FindRepositoryRoot() {
        foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}) {
            var directory=new DirectoryInfo(start);
            while(directory is not null) {
                if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){return directory.FullName;}
                directory=directory.Parent;
            }
        }
        return Environment.CurrentDirectory;
    }

    private sealed record 計画行(VerificationCase Case,string 表示,string 補足);
    private sealed record 履歴行(VerificationRun Run,string 表示);
    private sealed record 結果行(CaseResult Result,ComparisonResult? Comparison,string 表示);
}

internal static class VerificationUiLayoutExtensions {
    public static Grid ConfigureRows(this Grid grid) {
        if(grid.Children.Count>1){grid.SetRow(grid.Children[1],1);}
        return grid;
    }

    public static string ProfileDisplay(this VerificationRun run)=>run.Plan.Profile;
}
