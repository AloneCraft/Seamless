namespace Test.Seamless.Collections;
/// <summary>
/// HashSet �e�X�g�̋��ʊ��N���X�B
/// Dictionary �� DictionaryTestBase �Ɠ��l�ɁA
/// ����� ITsavoriteHashSet&lt;T&gt; �𒼐ڌĂяo���B
/// </summary>
public abstract class HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    [Fact, Trait("Category","Unit")]
    public void IsReadOnly() {
        using var h = TStrategy.CreateMemorySet();
        ICollection<T> set = h.Set;
        {
            // �ǂݎ���p�ł͂Ȃ�����
            Assert.False(set.IsReadOnly);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Add() {
        {
            // �V�K�v�f�̒ǉ��� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // �d���v�f�̒ǉ��� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
            Assert.False(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // ICollection.Add �o�R�ł�ǉ��ł��邱��
            using var h = TStrategy.CreateMemorySet();
            ICollection<T> set = h.Set;
            set.Add(TStrategy.Elem(1));
            Assert.Single(set);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo() {
        {
            // �S�v�f��z��փR�s�[�ł��邱��
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            var array = new T[2];
            h.Set.CopyTo(array,0);
            Assert.Contains(TStrategy.Elem(1),array);
            Assert.Contains(TStrategy.Elem(2),array);
        }
        {
            // null �z��͗�O�ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.CopyTo(null!,0));
        }
        {
            // ���̃C���f�b�N�X�͗�O�ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Set.CopyTo(new T[2],-1));
        }
        {
            // �z�񂪏���������Ɨ�O�ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            Assert.Throws<ArgumentException>(() => h.Set.CopyTo(new T[1],0));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ExceptWith() {
        {
            // ���ʗv�f���폜���ꍷ�W���ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            h.Set.ExceptWith(other.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(2)));
        }
        {
            // null ��n���Ɨ�O�ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.ExceptWith((global::Seamless.Collections.Tsavorite.HashSet<T>)null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IntersectWith() {
        {
            // ���ʗv�f�������c�邱��
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(3));
            h.Set.IntersectWith(other.Set);
            Assert.False(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
            Assert.Single(h.Set);
        }
        {
            // null ��n���Ɨ�O�ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.IntersectWith((global::Seamless.Collections.Tsavorite.HashSet<T>)null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsSubsetOf() {
        {
            // �����W���Ȃ� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsSubsetOf(other.Set));
        }
        {
            // �܂܂�Ȃ��v�f������� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(99));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsSubsetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsSupersetOf() {
        {
            // ��ʏW���Ȃ� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsSupersetOf(other.Set));
        }
        {
            // �܂܂�Ȃ��v�f������� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(99));
            Assert.False(h.Set.IsSupersetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf() {
        {
            // �^�����W���Ȃ� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsProperSubsetOf(other.Set));
        }
        {
            // ���T�C�Y�ł� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsProperSubsetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf() {
        {
            // �^��ʏW���Ȃ� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsProperSupersetOf(other.Set));
        }
        {
            // ���T�C�Y�ł� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsProperSupersetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Overlaps() {
        {
            // ���ʗv�f������� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.Overlaps(other.Set));
        }
        {
            // ���ʗv�f���Ȃ���� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(99));
            Assert.False(h.Set.Overlaps(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void SetEquals() {
        {
            // ����v�f�Ȃ� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.SetEquals(other.Set));
        }
        {
            // �قȂ�v�f�Ȃ� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.False(h.Set.SetEquals(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWith() {
        {
            // ���ʗv�f��������Ώ̍��ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(3));
            h.Set.SymmetricExceptWith(other.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(2)));
            Assert.True(h.Set.Contains(TStrategy.Elem(3)));
        }
        {
            // null ��n���Ɨ�O�ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.SymmetricExceptWith((global::Seamless.Collections.Tsavorite.HashSet<T>)null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void UnionWith() {
        {
            // �a�W���ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            h.Set.UnionWith(other.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
            Assert.Equal(2,h.Set.Count);
        }
        {
            // null ��n���Ɨ�O�ɂȂ邱��
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.UnionWith((global::Seamless.Collections.Tsavorite.HashSet<T>)null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void WithOverloads_AcceptConcreteHashSetSystemISetAndEnumerable() {
        {
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            h.Set.ExceptWith(other.Set);
            Assert.Single(h.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            global::System.Collections.Generic.ISet<T> other = new global::System.Collections.Generic.HashSet<T> { TStrategy.Elem(2),TStrategy.Elem(3) };
            h.Set.IntersectWith(other);
            Assert.Single(h.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
        }
        {
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            h.Set.UnionWith(new[] { TStrategy.Elem(2),TStrategy.Elem(3),TStrategy.Elem(3) }.AsEnumerable());
            Assert.Equal(3,h.Set.Count);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
            Assert.True(h.Set.Contains(TStrategy.Elem(3)));
        }
        {
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            h.Set.SymmetricExceptWith(new[] { TStrategy.Elem(2),TStrategy.Elem(3),TStrategy.Elem(3) }.AsEnumerable());
            Assert.Equal(2,h.Set.Count);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(3)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Contains() {
        {
            // ���݂���v�f�ɂ� true ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // ���݂��Ȃ��v�f�ɂ� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Contains(TStrategy.Elem(99)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove() {
        {
            // ���݂���v�f�͍폜����邱��
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Remove(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // ���݂��Ȃ��v�f�� false ��Ԃ�����
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Remove(TStrategy.Elem(99)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Clear() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        {
            // Clear ��͋�W���ɂȂ邱��
            h.Set.Clear();
            Assert.Empty(h.Set);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Count() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        {
            // �v�f����Ԃ�����
            Assert.Equal(2,h.Set.Count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void GetEnumerator() {
        {
            // �W�F�l���b�N�񋓂őS�v�f�𑖍��ł��邱��
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            var list = new List<T>();
            foreach(var item in h.Set)
                list.Add(item);
            Assert.Equal(2,list.Count);
            Assert.Contains(TStrategy.Elem(1),list);
            Assert.Contains(TStrategy.Elem(2),list);
        }
        {
            // ��W�F�l���b�N�񋓂ł�����ł��邱��
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            IEnumerable set = h.Set;
            var count = 0;
            foreach(var _ in set)
                count++;
            Assert.Equal(1,count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose() {
        var h = TStrategy.CreateMemorySet();
        var set = h.Set;
        {
            // Dispose �͕�����Ă�ł���S�Ȃ���
            Assert.False(set.IsDisposed);
            set.Dispose();
            Assert.True(set.IsDisposed);
            set.Dispose();
            h.Dispose();
        }
    }
}
