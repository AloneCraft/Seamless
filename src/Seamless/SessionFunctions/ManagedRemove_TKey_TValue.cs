namespace Seamless.SessionFunctions;
/// <summary>
/// RMWで値の読み取りと削除を同時に行うセッション関数。
/// キーが存在する場合は値をOutputにコピーしてからExpireAndStopで論理削除する。
/// キーが存在しない場合はNeedInitialUpdate=falseでNotFoundを返す。
/// </summary>
internal readonly  struct ManagedRemove_TKey_TValue;
