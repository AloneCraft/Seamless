# 止めている作業の作りかけ

- `20261116-段階5-行の一覧の固定.patch`：Manager の照合の段階5（行の一覧の固定と表の版の検出）の作りかけ。Manager の DB は版付きの保存を使っておらず、版付きにすると `db.X` の直接読みが例外になるため止めている（統一仕様の手順 C・D 待ち）。もとは `F:\Seamless\wtm-work\parked\phase5-row-snapshot.patch`。F:\Seamless を片づけるために、ここへ移した（2026-11-17）。当てるときは `git apply` で、今の作業ツリーとの差を確かめる。
