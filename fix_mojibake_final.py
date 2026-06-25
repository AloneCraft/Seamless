"""
最終的な文字化け修正スクリプト

分析結果: ファイルの日本語コメントはUTF-8のバイト列がWindows-1252(cp1252)として
誤デコードされたmojibake状態。

例: 「コレクション」のUTF-8バイト「E3 82 B3 E3 83 AC...」が
cp1252では「ăłăŹ...」と表示される。

修復方法:
1. ファイルをUTF-8で読む → "ăŹ..." のようなmojibakeテキスト
2. cp1252/Latin-1でエンコードして元のUTF-8バイト列を復元
3. UTF-8でデコードして正しい日本語テキストを取得
"""
import pathlib

FILES = [
    "Enumerator.cs",
    "HashSetTestBase.cs",
    "IDictionaryTestBase.cs",
    "StoreCheckpoint.cs",
    "HashSetDebugView.cs",
]

BASE = pathlib.Path("g:/GitHub/Seamless/Tsavorite.Collection.Shared")
REPORT_PATH = pathlib.Path("g:/GitHub/Seamless/fix_final_report.txt")

def recover_from_mojibake(text: str) -> str | None:
    """mojibakeテキストから正しい日本語テキストを復元"""
    # 各文字をcp1252のバイトに変換して、その後UTF-8でデコード
    result = bytearray()
    for ch in text:
        cp = ord(ch)
        if cp < 0x80:
            # ASCII: そのまま
            result.append(cp)
        elif cp < 0x100:
            # Latin-1範囲: 直接マッピング
            result.append(cp)
        else:
            # cp1252でエンコード可能な文字
            try:
                b = ch.encode('cp1252')
                result.extend(b)
            except UnicodeEncodeError:
                try:
                    b = ch.encode('latin-1')
                    result.extend(b)
                except:
                    return None  # 変換不能

    try:
        return result.decode('utf-8')
    except:
        return None

def format_fix_result(original: str, fixed: str, max_lines: int = 5) -> list[str]:
    """修正結果を整形"""
    lines = []
    orig_lines = original.split('\n')
    fixed_lines = fixed.split('\n')

    count = 0
    for i, (ol, fl) in enumerate(zip(orig_lines, fixed_lines)):
        if '//' in fl and any(ord(c) > 0x7F for c in fl):
            lines.append(f"    L{i+1}: {fl.strip()[:120]}")
            count += 1
            if count >= max_lines:
                remaining = sum(1 for ol2, fl2 in zip(orig_lines[i+1:], fixed_lines[i+1:]) if '//' in fl2 and any(ord(c) > 0x7F for c in fl2))
                if remaining > 0:
                    lines.append(f"    ... and {remaining} more lines")
                break

    if count == 0:
        lines.append("    (no Japanese comments found)")

    return lines

report_lines = []

for fn in FILES:
    path = BASE / fn
    if not path.exists():
        report_lines.append(f"{fn}: SKIP (not found)")
        continue

    raw = path.read_bytes()
    report_lines.append(f"\n=== {fn} ({len(raw)} bytes) ===")

    # ファイルをUTF-8として読む
    try:
        text = raw.decode('utf-8')
        report_lines.append(f"  UTF-8: VALID")
    except:
        report_lines.append(f"  UTF-8: INVALID - CP932として処理")
        # CP932でデコード
        text = raw.decode('cp932', errors='replace')
        report_lines.append(f"  CP932: {len(text)} chars")

    # mojibake修復
    fixed = recover_from_mojibake(text)
    if fixed:
        jp_count = sum(1 for c in fixed if '\u3040' <= c <= '\u30FF' or '\u4E00' <= c <= '\u9FFF')
        report_lines.append(f"  正しい日本語文字数: {jp_count}")

        # 結果表示
        result_lines = format_fix_result(text, fixed)
        report_lines.extend(result_lines)

        if jp_count > 0:
            # バックアップ
            bak = path.with_suffix('.bak')
            if not bak.exists():
                path.rename(bak)
                report_lines.append(f"  Backup: {bak.name}")

            # 修正ファイルを書き込み
            path.write_bytes(fixed.encode('utf-8'))
            report_lines.append(f"  ✓ 修正完了 ({len(raw)} → {len(fixed.encode('utf-8'))} bytes)")
        else:
            # 日本語が含まれていない場合、変更なし
            report_lines.append(f"  ✓ 修正必要なし")
    else:
        report_lines.append(f"  ✗ 修復失敗")

report_lines.append("\n=== 完了 ===")

REPORT_PATH.write_text('\n'.join(report_lines), encoding='utf-8')
print(f"Report: {REPORT_PATH}")
