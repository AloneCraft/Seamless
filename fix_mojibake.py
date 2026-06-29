"""
文字化け修正スクリプト
Shared/Tsavorite.Collections の .cs ファイルの日本語コメントが文字化けしているのを修復する

文字化けパターン分析:
オリジナルのUTF-8日本語テキストが、何らかの経路でCP932(Shift-JIS)として解釈された後、
UTF-8に再変換されることで二重エンコード状態になっている。

修復アルゴリズム:
1. Latin-1(全バイト許容)でデコード → 各バイトをそのままUnicodeコードポイントにマッピング
2. 結果の文字列をCP932でエンコード → 元のShift-JISバイト列を復元
3. CP932デコードで日本語テキストに変換
4. 最終的にUTF-8でエンコードして保存
"""
import pathlib

FILES = [
    "Shared/Tsavorite.Collections/Enumerator.cs",
    "Shared/Tsavorite.Collections/HashSetTestBase.cs",
    "Shared/Tsavorite.Collections/IDictionaryTestBase.cs",
    "Shared/Tsavorite.Collections/StoreCheckpoint.cs",
    "Shared/Tsavorite.Collections/HashSetDebugView.cs",
]

def attempt_fix_v1_latin1_cp932_utf8(raw: bytes) -> tuple[bool, bytes]:
    """Latin-1でデコード→CP932で再エンコード→UTF-8保存"""
    try:
        # Latin-1: 各バイトをそのままUnicodeコードポイントにマップ
        text_l1 = raw.decode('latin-1')
        # CP932: 元のShift-JISバイトに変換
        bytes_cp932 = text_l1.encode('cp932')
        # CP932: 日本語テキストとしてデコード
        text_jp = bytes_cp932.decode('cp932')
        # UTF-8: ファイル保存用エンコード
        result = text_jp.encode('utf-8')
        # 有効なUTF-8か確認
        result.decode('utf-8')
        return True, result
    except Exception as e:
        return False, raw

def attempt_fix_v2_ignore_invalid(raw: bytes) -> tuple[bool, bytes]:
    """CP932でデコード（エラー置換）→日本語テキスト取得→UTF-8エンコード"""
    try:
        text = raw.decode('cp932', errors='replace')
        result = text.encode('utf-8')
        result.decode('utf-8')
        return True, result
    except:
        return False, raw

def attempt_fix_v3_double_encoding(raw: bytes) -> tuple[bool, bytes]:
    """
    UTF-8バイト列がWindows-1252として誤解釈され、その文字列が
    さらにUTF-8エンコードされた状態を修復する。
    """
    try:
        # Windows-1252としてデコード
        text_w1252 = raw.decode('windows-1252')
        # Latin-1として再エンコード（全バイト維持）
        bytes_l1 = text_w1252.encode('latin-1')
        # UTF-8としてデコード
        text_utf8 = bytes_l1.decode('utf-8')
        result = text_utf8.encode('utf-8')
        return True, result
    except:
        return False, raw

def attempt_fix_v4_chunked(raw: bytes) -> tuple[bool, bytes]:
    """
    複雑な混在ケース: UTF-8有効部分はそのまま、CP932の残骸は変換
    """
    result = bytearray()
    i = 0
    while i < len(raw):
        b = raw[i]
        if b < 0x80:
            # ASCII: そのまま
            result.append(b)
            i += 1
        elif (b & 0xE0) == 0xC0:
            # 2-byte UTF-8 lead: 次のバイトが10xxxxxxか確認
            if i + 1 < len(raw) and (raw[i+1] & 0xC0) == 0x80:
                # 有効な2バイトUTF-8
                result.extend(raw[i:i+2])
                i += 2
            else:
                # 無効: CP932の可能性
                if i + 1 < len(raw):
                    cp_pair = bytes([b, raw[i+1]])
                    try:
                        decoded = cp_pair.decode('cp932')
                        result.extend(decoded.encode('utf-8'))
                        i += 2
                    except:
                        result.append(b)
                        i += 1
                else:
                    result.append(b)
                    i += 1
        elif (b & 0xF0) == 0xE0:
            # 3-byte UTF-8 lead
            if i + 2 < len(raw) and (raw[i+1] & 0xC0) == 0x80 and (raw[i+2] & 0xC0) == 0x80:
                result.extend(raw[i:i+3])
                i += 3
            else:
                # CP932の可能性: 2バイトでトライ
                if i + 1 < len(raw):
                    cp_pair = bytes([b, raw[i+1]])
                    try:
                        decoded = cp_pair.decode('cp932')
                        result.extend(decoded.encode('utf-8'))
                        i += 2
                    except:
                        result.append(b)
                        i += 1
                else:
                    result.append(b)
                    i += 1
        elif (b & 0xF8) == 0xF0:
            # 4-byte UTF-8 lead
            if i + 3 < len(raw) and (raw[i+1] & 0xC0) == 0x80 and (raw[i+2] & 0xC0) == 0x80 and (raw[i+3] & 0xC0) == 0x80:
                result.extend(raw[i:i+4])
                i += 4
            else:
                result.append(b)
                i += 1
        else:
            # 1xxx xxxx but not a valid lead byte → CP932 2-byte start
            if i + 1 < len(raw):
                cp_pair = bytes([b, raw[i+1]])
                try:
                    decoded = cp_pair.decode('cp932')
                    result.extend(decoded.encode('utf-8'))
                    i += 2
                except:
                    result.append(b)
                    i += 1
            else:
                result.append(b)
                i += 1

    final = bytes(result)
    try:
        final.decode('utf-8')
        return True, final
    except:
        return False, raw

def fix_file(filepath: str) -> bool:
    path = pathlib.Path(filepath)
    raw = path.read_bytes()
    original = raw

    fixers = [
        ("V1 Latin-1→CP932→UTF-8", attempt_fix_v1_latin1_cp932_utf8),
        ("V2 CP932直接", attempt_fix_v2_ignore_invalid),
        ("V3 Windows-1252→Latin-1→UTF-8", attempt_fix_v3_double_encoding),
        ("V4 チャンク処理", attempt_fix_v4_chunked),
    ]

    print(f"\n=== {filepath} ===")
    for name, fixer in fixers:
        success, result = fixer(raw)
        if success and result != raw:
            # 結果が有効なUTF-8で、元と異なれば成功
            text = result.decode('utf-8')
            # 日本語を含む行を表示
            jp_lines = [l for l in text.split('\n') if any('\u3000' <= c <= '\u30FF' or '\u4E00' <= c <= '\u9FFF' for c in l)]
            print(f"  {name}: 成功! ({len(raw)}→{len(result)} bytes)")
            for l in jp_lines[:3]:
                print(f"    {l.strip()[:100]}")

            # バックアップ作成
            bak_path = path.with_suffix('.cs.bak')
            if not bak_path.exists():
                path.rename(bak_path)
                print(f"  バックアップ: {bak_path.name}")

            path.write_bytes(result)
            return True

    print(f"  ★ どの修復方法も失敗しました")
    return False

if __name__ == "__main__":
    for f in FILES:
        fix_file(f)
