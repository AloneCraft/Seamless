namespace Seamless.Stores;
/// <summary>
/// new() 制約の代替。リフレクションを回避し、静的ディスパッチでインスタンスを生成する。
/// 実装する型はプライベートコンストラクタを持つこと。
/// </summary>
/// <typeparam name="TSelf">実装する型自身</typeparam>
public interface ICreate<out TSelf>
    where TSelf:ICreate<TSelf>{
    static abstract TSelf Create();
}
