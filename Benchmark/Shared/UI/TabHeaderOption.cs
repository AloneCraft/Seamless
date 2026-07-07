using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class TabHeaderOption:TabHeaderOptionBase{
    public TabHeaderOption(string headerText){
        this.HeaderText=headerText;
    }

    public override string HeaderText { get; }
}
