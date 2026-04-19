using Seamless;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.Unit.Helpers.Collections;
/// <summary>
/// <see cref="ISeamlessDictionary{TKey,TValue}"/> �ŗL�̃e�X�g���N���X�B
/// <see cref="IDictionary{TKey,TValue}"/> �݊��e�X�g�� <see cref="IDictionaryTestBase{TStrategy,TKey,TValue}"/> �ɏW�񂳂�Ă���A
/// ���̃N���X�͂����p������ TryAdd�ARemove_OutValue�ADispose�AEntryCount ��ǉ�����B
/// </summary>
public abstract class DictionaryTestBase<TStrategy, TKey, TValue>:IDictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private static string CreateDiskFolder() =>
        Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    [Fact, Trait("Category","Unit")]
    public void TryAdd(){
        {
            // �V�K�L�[�̒ǉ��� Success
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TryAddResult.Success,result);
        }
        {
            // �d���L�[�̒ǉ��� KeyAlreadyExists
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var result = dict.TryAdd(TStrategy.K(1),TStrategy.V(2));
            Assert.Equal(TryAddResult.KeyAlreadyExists,result);
        }
        {
            // �R�[���h�f�[�^�ǂݍ��݌�̏d���ǉ��ł� KeyAlreadyExists
            var folder = CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice);
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(0),TStrategy.V(0));
            sh.FlushAndEvict(true);
            Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(TStrategy.K(0),TStrategy.V(1)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove_key_value(){
        {
            // ���݂���L�[�͍폜�l��Ԃ�
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(TStrategy.K(1),out var value);
            Assert.True(removed);
            Assert.Equal(TStrategy.V(1),value);
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // ���݂��Ȃ��L�[�� false �� default ��Ԃ�
            using var sh = TStrategy.CreateMemoryStore();
            using var dict = sh.NewDict();
            var removed = dict.Remove(TStrategy.K(99),out var value);
            Assert.False(removed);
            Assert.Equal(default,value);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose(){
        var sh = TStrategy.CreateMemoryStore();
        var dict = sh.NewDict();
        {
            // Dispose �͕�����Ă�ł���S�Ȃ���
            Assert.False(dict.IsDisposed);
            dict.Dispose();
            Assert.True(dict.IsDisposed);
            dict.Dispose();
            sh.Dispose();
        }
    }
    [Fact, Trait("Category","Unit")]
    public void EntryCount(){
        using var sh = TStrategy.CreateMemoryStore();
        using var dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        {
            // �X�g�A��̌����� 1 �ȏ�ł��邱��
            Assert.True(dict.EntryCount>=1);
        }
    }
}
