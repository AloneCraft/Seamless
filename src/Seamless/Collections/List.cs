using System.Collections;

namespace Seamless.Collections;

public abstract class List<T>:IReadOnlyList<T>, IDisposable {
    public string Path { get; }
    public long Count { get; protected set; }
    public long Capacity { get; protected set; }
    public bool IsDisposed { get; private set; }

    int IReadOnlyCollection<T>.Count => checked((int)this.Count);
    T IReadOnlyList<T>.this[int index] => this[index];

    public T this[int index] => this[(long)index];

    public T this[long index] {
        get {
            this.ThrowIfDisposed();
            if((ulong)index>=(ulong)this.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return this.GetItem(index);
        }
    }

    protected List(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directoryPath = System.IO.Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        this.Path=path;
    }

    public bool Remove() {
        this.ThrowIfDisposed();
        if(this.Count==0)
            return false;
        this.Count--;
        this.OnCountChanged();
        return true;
    }

    public Enumerator GetEnumerator() {
        this.ThrowIfDisposed();
        return new(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public void Dispose() {
        if(this.IsDisposed)
            return;
        this.IsDisposed=true;
        this.DisposeCore();
    }

    protected void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(this.IsDisposed,this);
    }

    protected abstract T GetItem(long index);
    protected abstract void OnCountChanged();
    protected abstract void DisposeCore();

    public struct Enumerator(List<T> list):IEnumerator<T> {
        private long index = -1;
        public T Current => list[this.index];
        object IEnumerator.Current => this.Current!;
        public bool MoveNext() {
            var nextIndex = this.index+1;
            if(nextIndex>=list.Count)
                return false;
            this.index=nextIndex;
            return true;
        }
        public void Reset() {
            this.index=-1;
        }
        public readonly void Dispose() {
        }
    }
}