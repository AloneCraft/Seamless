using Seamless.RelationStudio.ViewModel;

using Syncfusion.Maui.DataGrid;
namespace Seamless.RelationStudio;

public partial class MainPage:ContentPage {
    int count = 0;

    public MainPage() {
        InitializeComponent();
        var BindingContext=(Statistics)this.BindingContext;
        BindingContext.read();
    }

    private void OnCounterClicked(object sender,EventArgs e) {
        count++;
    }
}

