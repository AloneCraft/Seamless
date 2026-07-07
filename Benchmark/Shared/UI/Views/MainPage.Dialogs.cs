namespace UI;

public sealed partial class MainPage {
    private async Task<BenchmarkCloseAction> ShowCloseConfirmDialogAsync(bool isBenchmarkActive,bool isBenchmarkPaused,bool hasUnsavedResults) {
        var options = this.CreateCloseConfirmOptions(isBenchmarkActive,isBenchmarkPaused,hasUnsavedResults);
        var result = await this.ShowConfirmDialogAsync(
            "終了確認",
            CreateCloseConfirmMessage(isBenchmarkActive,isBenchmarkPaused,hasUnsavedResults),
            options);
        return Enum.TryParse<BenchmarkCloseAction>(result,out var action)
            ? action
            : BenchmarkCloseAction.Cancel;
    }

    private IReadOnlyList<ConfirmDialogOption> CreateCloseConfirmOptions(bool isBenchmarkActive,bool isBenchmarkPaused,bool hasUnsavedResults) {
        var options = BenchmarkCloseDialogLogic.CreateOptions(isBenchmarkActive,isBenchmarkPaused,hasUnsavedResults);
        return options
            .Select(option => new ConfirmDialogOption(option.Text,option.Action.ToString(),option.Kind==BenchmarkToolbarButtonRole.Stop
                ? ConfirmDialogButtonKind.Danger
                : ConfirmDialogButtonKind.Neutral))
            .ToArray();
    }

    private static string CreateCloseConfirmMessage(bool isBenchmarkActive,bool isBenchmarkPaused,bool hasUnsavedResults) {
        if(isBenchmarkPaused) {
            return "一時停止中のベンチマークを停止して終了しますか？";
        }

        if(isBenchmarkActive) {
            if(hasUnsavedResults) {
                return "実行中のベンチマークを中止し、未保存の結果をどう扱うか選んでください。";
            }

            return "実行中のベンチマークを中止して終了しますか？";
        }

        return hasUnsavedResults
            ? "未保存のベンチマーク結果があります。どう扱って終了しますか？"
            : "終了確認";
    }

    private Task<bool> ShowBooleanConfirmDialogAsync(string title,string message,string accept,string cancel) =>
        this.ShowConfirmDialogAsync(
            title,
            message,
            [
                new ConfirmDialogOption(accept,"accept",ConfirmDialogButtonKind.Primary),
                new ConfirmDialogOption(cancel,"cancel",ConfirmDialogButtonKind.Neutral),
            ])
            .ContinueWith(static task => task.Status==TaskStatus.RanToCompletion&&task.Result=="accept",TaskScheduler.Default);

    private Task<string> ShowConfirmDialogAsync(string title,string message,IReadOnlyList<ConfirmDialogOption> options) {
        var root = this.rootLayout;
        var fallbackValue = ResolveDialogFallbackValue(options);
        if(root is null||options.Count==0) {
            return Task.FromResult(fallbackValue);
        }

        this.CompleteActiveDialog(root,this.activeDialogFallbackValue);

        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = this.CreateConfirmDialogOverlay(title,message,options,value => {
            this.CompleteActiveDialog(root,value);
        });
        this.activeDialogOverlay=overlay;
        this.activeDialogCompletion=completion;
        this.activeDialogFallbackValue=fallbackValue;
        root.Add(overlay,0,0);
        Grid.SetRowSpan(overlay,2);
        return completion.Task;
    }

    private void CompleteActiveDialog(Grid root,string value) {
        if(this.activeDialogOverlay is not null) {
            root.Children.Remove(this.activeDialogOverlay);
            this.activeDialogOverlay=null;
        }

        var completion = this.activeDialogCompletion;
        this.activeDialogCompletion=null;
        this.activeDialogFallbackValue=string.Empty;
        completion?.TrySetResult(value);
    }

    private static string ResolveDialogFallbackValue(IReadOnlyList<ConfirmDialogOption> options) {
        var cancelOption = options.FirstOrDefault(static option =>
            string.Equals(option.Value,"Cancel",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(option.Value,"cancel",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(option.Text,"キャンセル",StringComparison.OrdinalIgnoreCase));
        return cancelOption?.Value??options.LastOrDefault()?.Value??string.Empty;
    }

    private View CreateConfirmDialogOverlay(
        string title,
        string message,
        IReadOnlyList<ConfirmDialogOption> options,
        Action<string> completed) {
        var overlay = new Grid {
            BackgroundColor=Color.FromRgba(15,23,42,110),
            Padding=new Thickness(24),
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=false,
        };
        var card = new Border {
            BackgroundColor=Colors.White,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=14,
            },
            Shadow=new Shadow {
                Brush=Brush.Black,
                Opacity=0.18f,
                Radius=18,
                Offset=new Point(0,8),
            },
            Padding=new Thickness(24,22),
            MaximumWidthRequest=860,
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
        };
        var content = new VerticalStackLayout {
            Spacing=18,
        };
        content.Children.Add(new Label {
            Text=title,
            FontSize=18,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.WordWrap,
        });
        if(!string.IsNullOrWhiteSpace(message)) {
            content.Children.Add(new Label {
                Text=message,
                FontSize=14,
                TextColor=Color.FromArgb("#334155"),
                HorizontalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.WordWrap,
            });
        }

        content.Children.Add(this.CreateConfirmDialogButtons(options,completed));
        card.Content=content;
        overlay.Children.Add(card);
        return overlay;
    }

    private View CreateConfirmDialogButtons(IReadOnlyList<ConfirmDialogOption> options,Action<string> completed) {
        var layout = new HorizontalStackLayout {
            Spacing=10,
            HorizontalOptions=LayoutOptions.Center,
        };

        for(var index = 0; index<options.Count; index++) {
            var option = options[index];
            var button = this.CreateConfirmDialogButton(option);
            button.Clicked+=(_,_) => completed(option.Value);
            layout.Children.Add(button);
        }

        return layout;
    }

    private Button CreateConfirmDialogButton(ConfirmDialogOption option) {
        var button = new Button {
            Text=option.Text,
            HeightRequest=42,
            MinimumWidthRequest=128,
            WidthRequest=168,
            CornerRadius=8,
            BorderWidth=1,
            FontSize=14,
            FontAttributes=FontAttributes.Bold,
            Padding=new Thickness(12,0),
        };
        switch(option.Kind) {
            case ConfirmDialogButtonKind.Primary:
                button.BackgroundColor=Color.FromArgb("#0F766E");
                button.BorderColor=Color.FromArgb("#0F766E");
                button.TextColor=Colors.White;
                break;
            case ConfirmDialogButtonKind.Danger:
                button.BackgroundColor=Color.FromArgb("#FEF2F2");
                button.BorderColor=Color.FromArgb("#FCA5A5");
                button.TextColor=Color.FromArgb("#991B1B");
                break;
            default:
                button.BackgroundColor=Colors.White;
                button.BorderColor=Color.FromArgb("#CBD5E1");
                button.TextColor=Color.FromArgb("#334155");
                break;
        }

        return button;
    }

    private enum ConfirmDialogButtonKind {
        Primary,
        Neutral,
        Danger,
    }

    private sealed record ConfirmDialogOption(string Text,string Value,ConfirmDialogButtonKind Kind);

}
