using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

internal sealed class ベンチマークデータグリッド更新バッチイベント引数:EventArgs {
    public static readonly ベンチマークデータグリッド更新バッチイベント引数 開始 = new(true);
    public static readonly ベンチマークデータグリッド更新バッチイベント引数 終了 = new(false);
    private ベンチマークデータグリッド更新バッチイベント引数(bool 開始時か) => this.開始時か=開始時か;
    public bool 開始時か { get; }
}
