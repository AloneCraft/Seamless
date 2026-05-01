#nullable enable

namespace Seamless.SourceGenerator.Generated
{
    // LINQ 呼び出し連鎖 (Select/Where/Take の連続) を単一ループに統合する fusion 基盤クラス群。
    // このファイルはユーザーコードに依存しない固定 support なので、source generator 出力ではなく静的ソースとして共有する。
    // generator は呼び出し位置ごとの interceptor と static lambda strategy だけを生成し、その生成コードからこの型群を呼び出す。
    internal static class LinqInvocationFusionSupport
    {
        // まだ列挙していない LINQ 連鎖を表す遅延ソース。
        // T は「現在の連鎖の結果型」で、Select を追加すると TResult の I融合ソース<TResult> へ型が進む。
        // ここでは public LINQ と同じ遅延実行を保ち、ToList/ToArray/Count 相当の実体化時だけ元 source を 1 回列挙する。
        internal interface I融合ソース<T> : global::System.Collections.Generic.IEnumerable<T>
        {
            // delegate path。capture あり lambda、メソッドグループ、generator が安全に移動できない式は Func を保持して呼び出す。
            I融合ソース<TResult> 選択追加<TResult>(
                global::System.Func<T, TResult> selector,
                [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(selector))] string? selector式 = null);

            // static strategy path。安全な static lambda は generator が struct strategy に変換し、Func インスタンスを経由せず静的に Invoke する。
            I融合ソース<TResult> 選択追加<TResult, TSelector>(
                string? selector式 = null)
                where TSelector : struct, I融合選択<T, TResult>;

            // delegate path の Where。predicate は実行時値として保持されるので、capture ありでも元の C# と同じ意味を保てる。
            I融合ソース<T> 絞込追加(
                global::System.Func<T, bool> predicate,
                [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(predicate))] string? predicate式 = null);

            // static strategy path の Where。TPredicate.Invoke を型パラメーター経由で呼ぶため、delegate allocation と間接呼び出しを避けやすい。
            I融合ソース<T> 絞込追加<TPredicate>(
                string? predicate式 = null)
                where TPredicate : struct, I融合絞込<T>;

            // Take の追加。count が 0 以下なら列挙開始前に空であることが確定する。
            I融合ソース<T> 先頭取得追加(
                int count,
                [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(count))] string? count式 = null);

            // source.Select(...).ToList() のような終端 Select を、既存の融合パイプラインに最後の射影だけ足して即時実体化する。
            global::System.Collections.Generic.List<TResult> 選択実体化<TResult>(global::System.Func<T, TResult> selector);

            // static strategy 版の終端 Select 実体化。連鎖途中の Select と同じく TSelector.Invoke を直接使う。
            global::System.Collections.Generic.List<TResult> 選択実体化<TResult, TSelector>()
                where TSelector : struct, I融合選択<T, TResult>;

            // 現在の連鎖結果を List<T> に実体化する。
            global::System.Collections.Generic.List<T> リスト実体化();

            // 現在の連鎖結果を配列に実体化する。配列化は List 実体化に集約し、列挙ロジックを重複させない。
            T[] 配列実体化();

            // DebuggerDisplay / Watch で見せる元 LINQ 式。実行計画ではなく、ユーザーが書いた式に近い文字列を残す。
            string ウォッチ式 { get; }
        }

        // generator が static lambda の本体を移した Select 用 strategy。
        // static abstract member にしておくことで strategy の値を保持せず、型引数だけで呼び出せる。
        internal interface I融合選択<T, TResult>
        {
            static abstract TResult Invoke(T value);
        }

        // generator が static lambda の本体を移した Where 用 strategy。
        // capture なし static lambda だけがここに来るため、Invoke は入力値だけで判定できる。
        internal interface I融合絞込<T>
        {
            static abstract bool Invoke(T value);
        }

        // projector が 1 個の入力要素を処理した結果。
        // default(State=0) は「この入力はスキップ」、State=1 は「Value を yield」、State=2 は「以降の列挙を停止」を表す。
        private readonly struct 融合ステップ<T>
        {
            private 融合ステップ(byte state, T value)
            {
                this.State = state;
                this.Value = value;
            }

            public byte State { get; }

            public T Value { get; }

            public bool HasValue => this.State == 1;

            public bool Stop => this.State == 2;

            public static 融合ステップ<T> Yield(T value) => new(1, value);

            public static 融合ステップ<T> StopStep() => new(2, default!);
        }

        // 元 source の 1 要素(TRoot)から、現在の連鎖結果(TCurrent)を 0 個または 1 個作る処理単位。
        // Where は default を返して 0 個、Select は型を変えて 1 個、Take は必要数に達したら StopStep を返す。
        private interface I融合プロジェクタ<TRoot, TCurrent>
        {
            bool CanContinue { get; }

            融合ステップ<TCurrent> Project(TRoot item);
        }

        // IEnumerable は複数回列挙できる必要があるため、状態を持つ projector は列挙ごとに作り直す。
        // factory は immutable な連鎖定義、projector は Take の remaining などを持つ列挙中の mutable state という役割分担。
        private interface I融合プロジェクタファクトリ<TRoot, TCurrent, TProjector>
            where TProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            bool CanStart { get; }

            TProjector Create();
        }

        // 連鎖の起点。元要素をそのまま現在値として流す。
        private readonly struct 恒等プロジェクタファクトリ<T> : I融合プロジェクタファクトリ<T, T, 恒等プロジェクタ<T>>
        {
            public bool CanStart => true;

            public 恒等プロジェクタ<T> Create() => default;
        }

        private struct 恒等プロジェクタ<T> : I融合プロジェクタ<T, T>
        {
            public bool CanContinue => true;

            public 融合ステップ<T> Project(T item) => 融合ステップ<T>.Yield(item);
        }

        // Take(0) など、列挙前から空が確定した連鎖を表す。
        // source.GetEnumerator() すら呼ばずに終端処理を空結果へ短絡するための projector。
        private readonly struct 空プロジェクタファクトリ<TRoot, TCurrent> : I融合プロジェクタファクトリ<TRoot, TCurrent, 空プロジェクタ<TRoot, TCurrent>>
        {
            public bool CanStart => false;

            public 空プロジェクタ<TRoot, TCurrent> Create() => default;
        }

        private struct 空プロジェクタ<TRoot, TCurrent> : I融合プロジェクタ<TRoot, TCurrent>
        {
            public bool CanContinue => false;

            public 融合ステップ<TCurrent> Project(TRoot item) => 融合ステップ<TCurrent>.StopStep();
        }

        // delegate を保持する Select。capture あり lambda など、元の Func を残す必要がある場合の通常 path。
        private readonly struct 選択プロジェクタファクトリ<TRoot, TCurrent, TResult, TPreviousFactory, TPreviousProjector> : I融合プロジェクタファクトリ<TRoot, TResult, 選択プロジェクタ<TRoot, TCurrent, TResult, TPreviousProjector>>
            where TPreviousFactory : struct, I融合プロジェクタファクトリ<TRoot, TCurrent, TPreviousProjector>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private readonly TPreviousFactory previousFactory;
            private readonly global::System.Func<TCurrent, TResult> selector;

            public 選択プロジェクタファクトリ(TPreviousFactory previousFactory, global::System.Func<TCurrent, TResult> selector)
            {
                this.previousFactory = previousFactory;
                this.selector = selector;
            }

            public bool CanStart => this.previousFactory.CanStart;

            public 選択プロジェクタ<TRoot, TCurrent, TResult, TPreviousProjector> Create() => new(this.previousFactory.Create(), this.selector);
        }

        private struct 選択プロジェクタ<TRoot, TCurrent, TResult, TPreviousProjector> : I融合プロジェクタ<TRoot, TResult>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private TPreviousProjector previousProjector;
            private readonly global::System.Func<TCurrent, TResult> selector;

            public 選択プロジェクタ(TPreviousProjector previousProjector, global::System.Func<TCurrent, TResult> selector)
            {
                this.previousProjector = previousProjector;
                this.selector = selector;
            }

            public bool CanContinue => this.previousProjector.CanContinue;

            public 融合ステップ<TResult> Project(TRoot item)
            {
                var step = this.previousProjector.Project(item);
                if (step.Stop)
                {
                    return 融合ステップ<TResult>.StopStep();
                }
                if (!step.HasValue)
                {
                    return default;
                }
                // 前段が値を出したときだけ selector を適用する。Where で落ちた入力には selector を呼ばない。
                return 融合ステップ<TResult>.Yield(this.selector(step.Value));
            }
        }

        // static lambda から生成した strategy を使う Select。
        // strategy のインスタンスは持たず、TSelector.Invoke を直接呼ぶので delegate path より軽い呼び出し形になる。
        private readonly struct 静的選択プロジェクタファクトリ<TRoot, TCurrent, TResult, TSelector, TPreviousFactory, TPreviousProjector> : I融合プロジェクタファクトリ<TRoot, TResult, 静的選択プロジェクタ<TRoot, TCurrent, TResult, TSelector, TPreviousProjector>>
            where TSelector : struct, I融合選択<TCurrent, TResult>
            where TPreviousFactory : struct, I融合プロジェクタファクトリ<TRoot, TCurrent, TPreviousProjector>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private readonly TPreviousFactory previousFactory;

            public 静的選択プロジェクタファクトリ(TPreviousFactory previousFactory)
            {
                this.previousFactory = previousFactory;
            }

            public bool CanStart => this.previousFactory.CanStart;

            public 静的選択プロジェクタ<TRoot, TCurrent, TResult, TSelector, TPreviousProjector> Create() => new(this.previousFactory.Create());
        }

        private struct 静的選択プロジェクタ<TRoot, TCurrent, TResult, TSelector, TPreviousProjector> : I融合プロジェクタ<TRoot, TResult>
            where TSelector : struct, I融合選択<TCurrent, TResult>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private TPreviousProjector previousProjector;

            public 静的選択プロジェクタ(TPreviousProjector previousProjector)
            {
                this.previousProjector = previousProjector;
            }

            public bool CanContinue => this.previousProjector.CanContinue;

            public 融合ステップ<TResult> Project(TRoot item)
            {
                var step = this.previousProjector.Project(item);
                if (step.Stop)
                {
                    return 融合ステップ<TResult>.StopStep();
                }
                if (!step.HasValue)
                {
                    return default;
                }
                // generator が生成した struct の static Invoke へ分岐する。
                return 融合ステップ<TResult>.Yield(TSelector.Invoke(step.Value));
            }
        }

        // delegate を保持する Where。predicate が false の場合は default step を返し、この入力だけをスキップする。
        private readonly struct 絞込プロジェクタファクトリ<TRoot, TCurrent, TPreviousFactory, TPreviousProjector> : I融合プロジェクタファクトリ<TRoot, TCurrent, 絞込プロジェクタ<TRoot, TCurrent, TPreviousProjector>>
            where TPreviousFactory : struct, I融合プロジェクタファクトリ<TRoot, TCurrent, TPreviousProjector>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private readonly TPreviousFactory previousFactory;
            private readonly global::System.Func<TCurrent, bool> predicate;

            public 絞込プロジェクタファクトリ(TPreviousFactory previousFactory, global::System.Func<TCurrent, bool> predicate)
            {
                this.previousFactory = previousFactory;
                this.predicate = predicate;
            }

            public bool CanStart => this.previousFactory.CanStart;

            public 絞込プロジェクタ<TRoot, TCurrent, TPreviousProjector> Create() => new(this.previousFactory.Create(), this.predicate);
        }

        private struct 絞込プロジェクタ<TRoot, TCurrent, TPreviousProjector> : I融合プロジェクタ<TRoot, TCurrent>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private TPreviousProjector previousProjector;
            private readonly global::System.Func<TCurrent, bool> predicate;

            public 絞込プロジェクタ(TPreviousProjector previousProjector, global::System.Func<TCurrent, bool> predicate)
            {
                this.previousProjector = previousProjector;
                this.predicate = predicate;
            }

            public bool CanContinue => this.previousProjector.CanContinue;

            public 融合ステップ<TCurrent> Project(TRoot item)
            {
                var step = this.previousProjector.Project(item);
                if (step.Stop || !step.HasValue)
                {
                    return step;
                }
                return this.predicate(step.Value) ? step : default;
            }
        }

        // static lambda から生成した strategy を使う Where。
        // predicate の意味は delegate path と同じだが、TPredicate.Invoke によって型引数ベースで呼び出す。
        private readonly struct 静的絞込プロジェクタファクトリ<TRoot, TCurrent, TPredicate, TPreviousFactory, TPreviousProjector> : I融合プロジェクタファクトリ<TRoot, TCurrent, 静的絞込プロジェクタ<TRoot, TCurrent, TPredicate, TPreviousProjector>>
            where TPredicate : struct, I融合絞込<TCurrent>
            where TPreviousFactory : struct, I融合プロジェクタファクトリ<TRoot, TCurrent, TPreviousProjector>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private readonly TPreviousFactory previousFactory;

            public 静的絞込プロジェクタファクトリ(TPreviousFactory previousFactory)
            {
                this.previousFactory = previousFactory;
            }

            public bool CanStart => this.previousFactory.CanStart;

            public 静的絞込プロジェクタ<TRoot, TCurrent, TPredicate, TPreviousProjector> Create() => new(this.previousFactory.Create());
        }

        private struct 静的絞込プロジェクタ<TRoot, TCurrent, TPredicate, TPreviousProjector> : I融合プロジェクタ<TRoot, TCurrent>
            where TPredicate : struct, I融合絞込<TCurrent>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private TPreviousProjector previousProjector;

            public 静的絞込プロジェクタ(TPreviousProjector previousProjector)
            {
                this.previousProjector = previousProjector;
            }

            public bool CanContinue => this.previousProjector.CanContinue;

            public 融合ステップ<TCurrent> Project(TRoot item)
            {
                var step = this.previousProjector.Project(item);
                if (step.Stop || !step.HasValue)
                {
                    return step;
                }
                return TPredicate.Invoke(step.Value) ? step : default;
            }
        }

        // Take(count)。前段が値を出した回数だけ remaining を減らし、0 になった時点で以降の列挙を停止する。
        private readonly struct 先頭取得プロジェクタファクトリ<TRoot, TCurrent, TPreviousFactory, TPreviousProjector> : I融合プロジェクタファクトリ<TRoot, TCurrent, 先頭取得プロジェクタ<TRoot, TCurrent, TPreviousProjector>>
            where TPreviousFactory : struct, I融合プロジェクタファクトリ<TRoot, TCurrent, TPreviousProjector>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private readonly TPreviousFactory previousFactory;
            private readonly int count;

            public 先頭取得プロジェクタファクトリ(TPreviousFactory previousFactory, int count)
            {
                this.previousFactory = previousFactory;
                this.count = count;
            }

            public bool CanStart => this.count > 0 && this.previousFactory.CanStart;

            public 先頭取得プロジェクタ<TRoot, TCurrent, TPreviousProjector> Create() => new(this.previousFactory.Create(), this.count);
        }

        private struct 先頭取得プロジェクタ<TRoot, TCurrent, TPreviousProjector> : I融合プロジェクタ<TRoot, TCurrent>
            where TPreviousProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            private TPreviousProjector previousProjector;
            private int remaining;

            public 先頭取得プロジェクタ(TPreviousProjector previousProjector, int count)
            {
                this.previousProjector = previousProjector;
                this.remaining = count;
            }

            public bool CanContinue => this.remaining > 0 && this.previousProjector.CanContinue;

            public 融合ステップ<TCurrent> Project(TRoot item)
            {
                if (this.remaining <= 0 || !this.previousProjector.CanContinue)
                {
                    return 融合ステップ<TCurrent>.StopStep();
                }
                var step = this.previousProjector.Project(item);
                if (step.Stop || !step.HasValue)
                {
                    return step;
                }
                // Where などでスキップされた入力は remaining を消費しない。実際に出力する値だけを Take の対象にする。
                this.remaining--;
                return step;
            }
        }

        // 遅延 LINQ 連鎖の実体。source は元の IEnumerable<TRoot> のまま保持し、projectorFactory が連鎖内容を型として持つ。
        // TResult へ進むたびに別の 融合ソース実装 型を作るが、source 自体は再列挙可能な元 sequence を共有する。
        [global::System.Diagnostics.DebuggerDisplay("{ウォッチ式,nq}")]
        private sealed class 融合ソース実装<TRoot, TCurrent, TProjectorFactory, TProjector> : I融合ソース<TCurrent>
            where TProjectorFactory : struct, I融合プロジェクタファクトリ<TRoot, TCurrent, TProjector>
            where TProjector : struct, I融合プロジェクタ<TRoot, TCurrent>
        {
            // source は常に連鎖の根。中間結果を List などへ materialize せず、終端まで 1 本の列挙で処理する。
            private readonly global::System.Collections.Generic.IEnumerable<TRoot> source;
            // 連鎖の処理内容。struct の入れ子にすることで、各 step の組み合わせを型として表す。
            private readonly TProjectorFactory projectorFactory;
            // DebuggerDisplay 用。パフォーマンス上の処理には使わず、デバッグ時に元式を追えるように保持する。
            private readonly string watchExpression;

            public 融合ソース実装(global::System.Collections.Generic.IEnumerable<TRoot> source, TProjectorFactory projectorFactory, string? watchExpression)
            {
                this.source = source;
                this.projectorFactory = projectorFactory;
                this.watchExpression = global::System.String.IsNullOrWhiteSpace(watchExpression) ? "source" : watchExpression!;
            }

            public string ウォッチ式 => this.watchExpression;

            public I融合ソース<TResult> 選択追加<TResult>(global::System.Func<TCurrent, TResult> selector, [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(selector))] string? selector式 = null)
            {
                global::System.ArgumentNullException.ThrowIfNull(selector);
                // 既存 factory を前段として包み、Project 時に selector を 1 段だけ後続適用する。
                var factory = new 選択プロジェクタファクトリ<TRoot, TCurrent, TResult, TProjectorFactory, TProjector>(this.projectorFactory, selector);
                return new 融合ソース実装<TRoot, TResult, 選択プロジェクタファクトリ<TRoot, TCurrent, TResult, TProjectorFactory, TProjector>, 選択プロジェクタ<TRoot, TCurrent, TResult, TProjector>>(this.source, factory, $"{this.watchExpression}.Select({FormatWatchArgument(selector式)})");
            }

            public I融合ソース<TResult> 選択追加<TResult, TSelector>(string? selector式 = null)
                where TSelector : struct, I融合選択<TCurrent, TResult>
            {
                // static strategy は型引数にだけ現れる。factory/projector に strategy value を持たせない。
                var factory = new 静的選択プロジェクタファクトリ<TRoot, TCurrent, TResult, TSelector, TProjectorFactory, TProjector>(this.projectorFactory);
                return new 融合ソース実装<TRoot, TResult, 静的選択プロジェクタファクトリ<TRoot, TCurrent, TResult, TSelector, TProjectorFactory, TProjector>, 静的選択プロジェクタ<TRoot, TCurrent, TResult, TSelector, TProjector>>(this.source, factory, $"{this.watchExpression}.Select({FormatWatchArgument(selector式)})");
            }

            public I融合ソース<TCurrent> 絞込追加(global::System.Func<TCurrent, bool> predicate, [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(predicate))] string? predicate式 = null)
            {
                global::System.ArgumentNullException.ThrowIfNull(predicate);
                // Where は型を変えないため TCurrent のまま、前段 projector の後ろに predicate を合成する。
                var factory = new 絞込プロジェクタファクトリ<TRoot, TCurrent, TProjectorFactory, TProjector>(this.projectorFactory, predicate);
                return new 融合ソース実装<TRoot, TCurrent, 絞込プロジェクタファクトリ<TRoot, TCurrent, TProjectorFactory, TProjector>, 絞込プロジェクタ<TRoot, TCurrent, TProjector>>(this.source, factory, $"{this.watchExpression}.Where({FormatWatchArgument(predicate式)})");
            }

            public I融合ソース<TCurrent> 絞込追加<TPredicate>(string? predicate式 = null)
                where TPredicate : struct, I融合絞込<TCurrent>
            {
                // delegate path と同じ連鎖構造で、predicate だけを static strategy に差し替える。
                var factory = new 静的絞込プロジェクタファクトリ<TRoot, TCurrent, TPredicate, TProjectorFactory, TProjector>(this.projectorFactory);
                return new 融合ソース実装<TRoot, TCurrent, 静的絞込プロジェクタファクトリ<TRoot, TCurrent, TPredicate, TProjectorFactory, TProjector>, 静的絞込プロジェクタ<TRoot, TCurrent, TPredicate, TProjector>>(this.source, factory, $"{this.watchExpression}.Where({FormatWatchArgument(predicate式)})");
            }

            public I融合ソース<TCurrent> 先頭取得追加(int count, [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(count))] string? count式 = null)
            {
                var watchExpression = $"{this.watchExpression}.Take({FormatWatchArgument(count式)})";
                if (count <= 0)
                {
                    // Take(0) 以下は元 source の副作用も含めて列挙しない、という LINQ の挙動に合わせて空 projector に切り替える。
                    return new 融合ソース実装<TRoot, TCurrent, 空プロジェクタファクトリ<TRoot, TCurrent>, 空プロジェクタ<TRoot, TCurrent>>(this.source, default, watchExpression);
                }
                var factory = new 先頭取得プロジェクタファクトリ<TRoot, TCurrent, TProjectorFactory, TProjector>(this.projectorFactory, count);
                return new 融合ソース実装<TRoot, TCurrent, 先頭取得プロジェクタファクトリ<TRoot, TCurrent, TProjectorFactory, TProjector>, 先頭取得プロジェクタ<TRoot, TCurrent, TProjector>>(this.source, factory, watchExpression);
            }

            public global::System.Collections.Generic.List<TResult> 選択実体化<TResult>(global::System.Func<TCurrent, TResult> selector)
            {
                global::System.ArgumentNullException.ThrowIfNull(selector);
                var result = new global::System.Collections.Generic.List<TResult>();
                if (!this.projectorFactory.CanStart)
                {
                    return result;
                }
                var projector = this.projectorFactory.Create();
                if (!projector.CanContinue)
                {
                    return result;
                }
                using var enumerator = this.source.GetEnumerator();
                while (projector.CanContinue && enumerator.MoveNext())
                {
                    // 元 source の 1 要素に対して、合成済み projector が Where/Select/Take を順に評価する。
                    var step = projector.Project(enumerator.Current);
                    if (step.Stop)
                    {
                        break;
                    }
                    if (step.HasValue)
                    {
                        result.Add(selector(step.Value));
                    }
                }
                return result;
            }

            public global::System.Collections.Generic.List<TResult> 選択実体化<TResult, TSelector>()
                where TSelector : struct, I融合選択<TCurrent, TResult>
            {
                var result = new global::System.Collections.Generic.List<TResult>();
                if (!this.projectorFactory.CanStart)
                {
                    return result;
                }
                var projector = this.projectorFactory.Create();
                if (!projector.CanContinue)
                {
                    return result;
                }
                using var enumerator = this.source.GetEnumerator();
                while (projector.CanContinue && enumerator.MoveNext())
                {
                    var step = projector.Project(enumerator.Current);
                    if (step.Stop)
                    {
                        break;
                    }
                    if (step.HasValue)
                    {
                        // 終端 Select も static strategy にできるときは delegate を作らず直接 Invoke する。
                        result.Add(TSelector.Invoke(step.Value));
                    }
                }
                return result;
            }

            public global::System.Collections.Generic.List<TCurrent> リスト実体化()
            {
                var result = new global::System.Collections.Generic.List<TCurrent>();
                if (!this.projectorFactory.CanStart)
                {
                    return result;
                }
                var projector = this.projectorFactory.Create();
                if (!projector.CanContinue)
                {
                    return result;
                }
                using var enumerator = this.source.GetEnumerator();
                while (projector.CanContinue && enumerator.MoveNext())
                {
                    var step = projector.Project(enumerator.Current);
                    if (step.Stop)
                    {
                        break;
                    }
                    if (step.HasValue)
                    {
                        result.Add(step.Value);
                    }
                }
                return result;
            }

            public TCurrent[] 配列実体化() => this.リスト実体化().ToArray();

            public global::System.Collections.Generic.IEnumerator<TCurrent> GetEnumerator()
            {
                if (!this.projectorFactory.CanStart)
                {
                    yield break;
                }
                var projector = this.projectorFactory.Create();
                if (!projector.CanContinue)
                {
                    yield break;
                }
                using var enumerator = this.source.GetEnumerator();
                while (projector.CanContinue && enumerator.MoveNext())
                {
                    // IEnumerable として使われた場合も、実体化メソッドと同じ projector を通して遅延 yield する。
                    var step = projector.Project(enumerator.Current);
                    if (step.Stop)
                    {
                        yield break;
                    }
                    if (step.HasValue)
                    {
                        yield return step.Value;
                    }
                }
            }

            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

            private static string FormatWatchArgument(string? argumentExpression)
            {
                // CallerArgumentExpression が取れない呼び出しでも Watch 表示が空欄にならないようにする。
                return global::System.String.IsNullOrWhiteSpace(argumentExpression) ? "?" : argumentExpression!;
            }
        }

        // generator が LINQ 呼び出しの先頭で挿入する entry point。
        // すでに融合ソースであれば二重に包まず返し、そうでなければ恒等 projector から連鎖を開始する。
        internal static I融合ソース<T> Create<T>(
            global::System.Collections.Generic.IEnumerable<T> source,
            [global::System.Runtime.CompilerServices.CallerArgumentExpression(nameof(source))] string? source式 = null)
        {
            global::System.ArgumentNullException.ThrowIfNull(source);
            if (source is I融合ソース<T> 既存融合ソース)
            {
                return 既存融合ソース;
            }
            return new 融合ソース実装<T, T, 恒等プロジェクタファクトリ<T>, 恒等プロジェクタ<T>>(source, default, source式);
        }
    }
}
