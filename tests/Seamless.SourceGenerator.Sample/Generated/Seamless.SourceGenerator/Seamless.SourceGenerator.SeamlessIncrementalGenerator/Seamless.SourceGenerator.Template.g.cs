#nullable enable
namespace System.Runtime.CompilerServices {
    [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]
    file sealed class InterceptsLocationAttribute:global::System.Attribute {
        public InterceptsLocationAttribute(string filePath,int line,int character) {
            this.FilePath=filePath;
            this.Line=line;
            this.Character=character;
        }

        public string FilePath { get; }
        public int Line { get; }
        public int Character { get; }
    }
}

namespace Seamless.SourceGenerator.Generated {
    internal static class ListWhereInterceptors {
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute("G:\\GitHub\\Seamless\\Seamless\\tests\\Seamless.SourceGenerator.Sample\\Program.cs",6,25)]
        public static global::System.Collections.Generic.IEnumerable<int> OptimizedWhere_Program_6_25(this global::System.Collections.Generic.IEnumerable<int> source,global::System.Func<int,bool> predicate) {
            global::System.Console.WriteLine("Seamless interceptor: OptimizedWhere_Program_6_25 (6:25)");
            return new OptimizedWhereEnumerable_Program_6_25((global::System.Collections.Generic.List<int>)source,predicate);
        }

        public readonly struct OptimizedWhereEnumerable_Program_6_25:global::System.Collections.Generic.IEnumerable<int> {
            private readonly global::System.Collections.Generic.List<int> source;
            private readonly global::System.Func<int,bool> predicate;

            public OptimizedWhereEnumerable_Program_6_25(global::System.Collections.Generic.List<int> source,global::System.Func<int,bool> predicate) {
                this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));
                this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));
            }

            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public Enumerator GetEnumerator() => new(this.source,this.predicate);

            global::System.Collections.Generic.IEnumerator<int> global::System.Collections.Generic.IEnumerable<int>.GetEnumerator() {
                var enumerator = this.GetEnumerator();
                while(enumerator.MoveNext()) {
                    yield return enumerator.Current;
                }
            }

            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<int>)this).GetEnumerator();

            public struct Enumerator {
                private readonly global::System.Collections.Generic.List<int> source;
                private readonly global::System.Func<int,bool> predicate;
                private int index;

                public Enumerator(global::System.Collections.Generic.List<int> source,global::System.Func<int,bool> predicate) {
                    this.source=source;
                    this.predicate=predicate;
                    this.index=-1;
                    this.Current=default;
                }

                public int Current { get; private set; }

                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() {
                    while(true) {
                        var next = this.index+1;
                        if(next>=this.source.Count) {
                            return false;
                        }
                        this.index=next;
                        var n = this.source[next];
                        if(!(n > 2)) {
                            continue;
                        }
                        this.Current=n;
                        return true;
                    }
                }
            }
        }

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute("G:\\GitHub\\Seamless\\Seamless\\tests\\Seamless.SourceGenerator.Sample\\Program.cs",14,29)]
        public static global::System.Collections.Generic.IEnumerable<int> OptimizedWhere_Program_14_29(this global::System.Collections.Generic.IEnumerable<int> source,global::System.Func<int,bool> predicate) {
            global::System.Console.WriteLine("Seamless interceptor: OptimizedWhere_Program_14_29 (14:29)");
            return new OptimizedWhereEnumerable_Program_14_29((global::System.Collections.Generic.List<int>)source,predicate);
        }

        public readonly struct OptimizedWhereEnumerable_Program_14_29:global::System.Collections.Generic.IEnumerable<int> {
            private readonly global::System.Collections.Generic.List<int> source;
            private readonly global::System.Func<int,bool> predicate;

            public OptimizedWhereEnumerable_Program_14_29(global::System.Collections.Generic.List<int> source,global::System.Func<int,bool> predicate) {
                this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));
                this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));
            }

            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public Enumerator GetEnumerator() => new(this.source,this.predicate);

            global::System.Collections.Generic.IEnumerator<int> global::System.Collections.Generic.IEnumerable<int>.GetEnumerator() {
                var enumerator = this.GetEnumerator();
                while(enumerator.MoveNext()) {
                    yield return enumerator.Current;
                }
            }

            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<int>)this).GetEnumerator();

            public struct Enumerator {
                private readonly global::System.Collections.Generic.List<int> source;
                private readonly global::System.Func<int,bool> predicate;
                private int index;

                public Enumerator(global::System.Collections.Generic.List<int> source,global::System.Func<int,bool> predicate) {
                    this.source=source;
                    this.predicate=predicate;
                    this.index=-1;
                    this.Current=default;
                }

                public int Current { get; private set; }

                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() {
                    while(true) {
                        var next = this.index+1;
                        if(next>=this.source.Count) {
                            return false;
                        }
                        this.index=next;
                        var candidate = this.source[next];
                        if(!this.predicate(candidate)) {
                            continue;
                        }
                        this.Current=candidate;
                        return true;
                    }
                }
            }
        }

    }
}
