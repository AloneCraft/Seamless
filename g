# -*- coding: utf-8 -*-
"""Debug position 639 in Enumerator.cs"""
import pathlib

raw = pathlib.Path("g:/GitHub/Seamless/Shared/Tsavorite.Collections/Enumerator.cs").read_bytes()

with open("g:/GitHub/Seamless/debug_639.txt", "w", encoding="utf-8") as f:
    start = max(0, 620)
    end = min(len(raw), 680)
    f.write(f"Bytes around position 639 (len={len(raw)}):\n")
    for i in range(start, end, 16):
        chunk = raw[i:min(i+16, end)]
        hex_str = ' '.join(f'{b:02x}' for b in chunk)
        ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in chunk)
        f.write(f"  {i:5d}: {hex_str:<48s} {ascii_str}\n")

    # Now fix: CP932 decode whole file, encode back, decode as UTF-8 with errors='replace'
    f.write(f"\nAttempting CP932 decode...\n")
    try:
        text_cp = raw.decode('cp932')
        f.write(f"  CP932 decode OK: {len(text_cp)} chars\n")

        # Show the line around position 639
        lines = text_cp.split('\n')
        for i, line in enumerate(lines):
            f.write(f"  Line {i+1}: {line.strip()[:100]}\n")
            if i > 20:
                f.write(f"  ... (showing first 21 lines)\n")
                break

        # Try encoding back and decoding as UTF-8
        sjis = text_cp.encode('cp932')
        f.write(f"\n  SJIS bytes length: {len(sjis)}\n")

        # Show what's at position 639 in the bytes
        f.write(f"  SJIS bytes around position 639:\n")
        s = max(0, 630)
        e = min(len(sjis), 660)
        for i in range(s, e, 16):
            chunk = sjis[i:min(i+16, e)]
            hex_str = ' '.join(f'{b:02x}' for b in chunk)
            f.write(f"    {i:5d}: {hex_str}\n")

        # Now decode with errors='surrogateescape' and then clean up
        try:
            result = sjis.decode('utf-8')
            f.write(f"  UTF-8 decode: OK\n")
        except UnicodeDecodeError as ue:
            f.write(f"  UTF-8 decode failed at pos {ue.start}: {ue.reason}\n")
            f.write(f"  Hex around pos {ue.start}:\n")
            s = max(0, ue.start-8)
            e = min(len(sjis), ue.start+16)
            for i in range(s, e):
                if i >= 0 and i < len(sjis):
                    f.write(f"    {i}: {sjis[i]:02x}\n")
    except Exception as e:
        f.write(f"  FAILED: {e}")

print("Written to debug_639.txt")
