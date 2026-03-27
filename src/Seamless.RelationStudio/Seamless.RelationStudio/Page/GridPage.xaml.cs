using Syncfusion.Maui.DataGrid;
namespace Seamless.RelationStudio;

public partial class GridPage:ContentPage {
    int count = 0;

    public GridPage(Statistics stats) {
        InitializeComponent();
        BindingContext = stats;
        //stats.read();
    }

    private void OnCounterClicked(object sender,EventArgs e) {
        count++;
    }
}

