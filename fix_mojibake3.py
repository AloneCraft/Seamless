"""
文字化け修正 v3: バックアップからリストアして正しい方法で修正

問題分析:
元のファイルはUTF-8の日本語テキストに、CP932(Shift-JIS)領域のバイトが混入した状態。
これは UTF-8として不完全なバイト列になっている。

正しい修復方法:
1. ファイルをLatin-1(全バイト許容)で読む
2. 各バイトに対して:
   - ASCII (0x00-0x7F): そのまま
   - 有効なUTF-8マルチバイトシーケンス: そのまま
   - 無効なUTF-8バイト（CP932の2バイト文字の一部）: CP932として解釈してUTF-8に変換
"""
import pathlib

FILES = [
    ("Enumerator.cs", ".bak"),
    ("HashSetTestBase.cs", ".bak"),
    ("IDictionaryTestBase.cs", ".bak"),
    ("StoreCheckpoint.cs", ".bak"),
    ("HashSetDebugView.cs", ".bak"),
]

BASE = pathlib.Path("g:/GitHub/Seamless/Tsavorite.Collection.Shared")

def is_valid_utf8_start(b: int) -> bool:
    """UTF-8リードバイトかどうか判定"""
    return (b & 0x80) == 0 or (b & 0xE0) == 0xC0 or (b & 0xF0) == 0xE0 or (b & 0xF8) == 0xF0

def utf8_seq_len(b: int) -> int:
    """UTF-8リードバイトからシーケンス長を取得"""
    if (b & 0x80) == 0: return 1
    if (b & 0xE0) == 0xC0: return 2
    if (b & 0xF0) == 0xE0: return 3
    if (b & 0xF8) == 0xF0: return 4
    return 1

def is_valid_utf8_seq(data: bytes, pos: int, length: int) -> bool:
    """指定位置からlengthバイトが有効なUTF-8シーケンスか"""
    if pos + length > len(data):
        return False
    if length == 1:
        return data[pos] < 0x80
    if length == 2:
        b1, b2 = data[pos], data[pos+1]
        return (b1 & 0xE0) == 0xC0 and (b2 & 0xC0) == 0x80
    if length == 3:
        b1, b2, b3 = data[pos], data[pos+1], data[pos+2]
        return (b1 & 0xF0) == 0xE0 and (b2 & 0xC0) == 0x80 and (b3 & 0xC0) == 0x80
    if length == 4:
        b1, b2, b3, b4 = data[pos], data[pos+1], data[pos+2], data[pos+3]
        return (b1 & 0xF8) == 0xF0 and (b2 & 0xC0) == 0x80 and (b3 & 0xC0) == 0x80 and (b4 & 0xC0) == 0x80
    return False

report = []

for fn, ext in FILES:
    bak_path = BASE / (fn + ext)
    current_path = BASE / fn

    if not bak_path.exists():
        # Try direct file
        if current_path.exists():
            bak_path = current_path
        else:
            report.append(f"{fn}: SKIP (not found)")
            continue

    raw = bak_path.read_bytes()
    report.append(f"\n=== {fn} (bak: {len(raw)} bytes) ===")

    # Phase 1: Parse byte-by-byte, keeping valid UTF-8 and converting CP932 residuals
    result = bytearray()
    i = 0
    cp932_decoded_count = 0
    utf8_preserved_count = 0

    while i < len(raw):
        b = raw[i]

        if b < 0x80:
            # ASCII: always keep as-is
            result.append(b)
            i += 1
            continue

        # Check if it's a valid UTF-8 multi-byte sequence
        seq_len = utf8_seq_len(b)
        if seq_len > 1 and is_valid_utf8_seq(raw, i, seq_len):
            # Keep valid UTF-8 as-is
            result.extend(raw[i:i+seq_len])
            utf8_preserved_count += 1
            i += seq_len
            continue

        # Try as CP932 2-byte sequence
        if i + 1 < len(raw):
            b2 = raw[i+1]
            cp_pair = bytes([b, b2])
            try:
                decoded = cp_pair.decode('cp932')
                result.extend(decoded.encode('utf-8'))
                cp932_decoded_count += 1
                i += 2
                continue
            except:
                pass

        # Last resort: keep byte as-is
        result.append(b)
        i += 1

    fixed = bytes(result)

    # Phase 2: Verify the result is valid UTF-8
    try:
        text = fixed.decode('utf-8')
        report.append(f"  Result: VALID UTF-8 ({len(raw)} -> {len(fixed)} bytes)")
        report.append(f"  UTF-8 preserved: {utf8_preserved_count}, CP932 decoded: {cp932_decoded_count}")

        # Show fixed comments
        lines = text.split('\n')
        for i, line in enumerate(lines):
            if ('//' in line or '///' in line) and any(ord(c) > 0x7F for c in line):
                report.append(f"  L{i+1}: {line.strip()[:120]}")
                if sum(1 for _ in filter(None, [l for l in lines if '//' in l and any(ord(c) > 0x7F for c in l)]) ) < 20:
                    pass  # Continue showing
                else:
                    if i > 10:
                        break

        # Write fixed file (backup already exists)
        current_path.write_bytes(fixed)
        report.append(f"  => WRITTEN!")

    except UnicodeDecodeError as e:
        report.append(f"  Result: INVALID UTF-8 - {e}")
        # Fall back to cp932 with replace
        text = raw.decode('cp932', errors='replace')
        current_path.write_bytes(text.encode('utf-8'))
        report.append(f"  => FALLBACK CP932 written")

# Write report
result_path = pathlib.Path("g:/GitHub/Seamless/fix_result3.txt")
result_path.write_text('\n'.join(report), encoding='utf-8')
print(f"Report written to {result_path}")
