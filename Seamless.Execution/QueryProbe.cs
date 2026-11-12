using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Seamless.Execution;

/// <summary>
/// FLinqの生成コードが計測点で呼ぶ口。本体は計測点を型引数(値型)で受け取る1本だけを生成し、
/// 計測しないときは<see cref="NoProbe"/>、計測するときは<see cref="RegionProbe"/>で呼ぶ。
/// JITは値型の型引数ごとに別の機械語を作るので、<see cref="NoProbe"/>の版では空の呼び出しが消える。
/// 計測の段階を増やすときは、この口を実装する値型を足す(本体のコピーは増やさない)。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IQueryProbe{
    /// <summary>計測の対象(集計など)が入力の値を1件受け取った。</summary>
    void Input();
}

/// <summary>計測しないときの口。何もしない。</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct NoProbe:IQueryProbe{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Input(){}
}

/// <summary>今の区間の計測(<see cref="QueryProfileRegionScope"/>)へ記録する口。</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct RegionProbe(QueryProfileRegionScope region):IQueryProbe{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Input()=>region.Input();
}
