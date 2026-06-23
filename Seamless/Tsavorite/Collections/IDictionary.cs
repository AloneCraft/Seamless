namespace Seamless.Tsavorite.Collections;

/// <summary>
/// Tsavorite �o�b�N�G���h������ Dictionary �̋��ʃC���^�[�t�F�[�X�B
/// <see cref="IDictionary{TKey,TValue}"/> �ɉ����A�Ǘ��E�A���}�l�[�W�����������L����
/// <see cref="IsDisposed"/>�A<see cref="EntryCount"/>�A<see cref="TryAdd"/>�A
/// <see cref="Remove(TKey,out TValue)"/> �����J����B
/// </summary>
public interface IDictionary<TKey, TValue>:global::System.Collections.Generic.IDictionary<TKey,TValue>, IReadOnlyDictionary<TKey,TValue>, IMemoryCollection<KeyValuePair<TKey,TValue>>, IDisposable {
    /// <summary>���̃C���X�^���X�����ɔj������Ă��邩�ǂ������擾����B</summary>
    bool IsDisposed { get; }
    /// <summary>
    /// ���� session �𒣂�ւ��āATsavorite ���ێ����Ă���Â� epoch ����Q�Ƃ��������B
    /// ��������� no-op �ŁAsession �𖾎��Ǘ�����������K�v�ɉ����ď㏑������B
    /// </summary>
    void Refresh() {
    }
    /// <summary>�X�g�A�Ɋi�[����Ă���G���g�������擾����B</summary>
    long EntryCount { get; }
    /// <summary>�����Ăяo���݊��̂��߂� long �����擾���\�b�h�B</summary>
    long GetLongCount() => EntryCount;
    /// <summary>�L�[�����݂��Ȃ��ꍇ�̂ݒǉ�����B</summary>
    TryAddResult TryAdd(TKey key,TValue value);
    /// <summary>�w�肵���L�[�����݂��邩�ǂ�����Ԃ��B</summary>
    bool Contains(TKey key) => ContainsKey(key);
    /// <summary>�L�[���폜���A�폜���ꂽ�l��Ԃ��B</summary>
    bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value);
    // IDictionary<TKey,TValue> �� IReadOnlyDictionary<TKey,TValue> �̗����ɓ��������o�[�����݂��邽�߁A
    // new �ŒP��̃X���b�g�Ƃ��Đ錾���A�Ăяo�����̂����܂�������������B
    new bool ContainsKey(TKey key);
    new bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value);
    new TValue this[TKey key] { get; set; }
    new int Count { get; }
    new ICollection<TKey> Keys { get; }
    new ICollection<TValue> Values { get; }
}
