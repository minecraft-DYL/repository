#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""读取字体文件的内部名称表，打印可用于 WinUI 的 FontFamily 写法。

为什么需要它：
  WinUI 的字体引用 `<FontFamily>ms-appx:///path/to/x.otf#Family</FontFamily>` 里
  `#` 后面必须是字体**内部**的 family 名（nameID 1 / 16），而不是文件名。
  CSS 的 @font-face 可以随便起名字，WinUI 不行。所以必须真的读一遍字体。

只依赖标准库（struct），不装 fonttools。

用法:
    python tools/font_info.py                 # 扫描 xaml-src 里引用到的字体
    python tools/font_info.py path/to/font.ttf ...
    python tools/font_info.py --markdown      # 输出可直接贴进文档的表格
"""

from __future__ import annotations

import struct
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
FONT_DIR = REPO / "dotnet" / "src" / "OreUI.WinUI" / "Assets" / "OreUI" / "Fonts"

# nameID -> 含义
NAME_IDS = {
    1: "family",
    2: "subfamily",
    4: "full",
    6: "postscript",
    16: "typoFamily",
    17: "typoSubfamily",
}


def read_names(path: Path) -> dict[int, dict[str, str]]:
    """返回 {nameID: {平台标签: 字符串}}。"""
    data = path.read_bytes()
    if len(data) < 12:
        raise ValueError("文件太小，不是字体")
    num_tables = struct.unpack(">H", data[4:6])[0]

    name_off = None
    for i in range(num_tables):
        off = 12 + i * 16
        tag = data[off:off + 4]
        if tag == b"name":
            name_off = struct.unpack(">I", data[off + 8:off + 12])[0]
            break
    if name_off is None:
        raise ValueError("找不到 name 表")

    fmt, count, string_off = struct.unpack(">HHH", data[name_off:name_off + 6])
    if fmt not in (0, 1):
        raise ValueError(f"不支持的 name 表格式 {fmt}")
    storage = name_off + string_off

    out: dict[int, dict[str, str]] = {}
    for i in range(count):
        rec = name_off + 6 + i * 12
        plat, enc, lang, nid, length, offset = struct.unpack(">HHHHHH", data[rec:rec + 12])
        if nid not in NAME_IDS:
            continue
        raw = data[storage + offset:storage + offset + length]
        try:
            if plat == 3 and enc in (1, 10):          # Windows, UTF-16BE
                text = raw.decode("utf-16-be", "ignore")
            elif plat == 1 and enc == 0:              # Mac Roman
                text = raw.decode("mac-roman", "ignore")
            else:
                continue
        except Exception:
            continue
        text = text.replace("\x00", "").strip()
        if not text:
            continue
        lang_tag = {0x409: "en", 0x804: "zh"}.get(lang, lang)
        out.setdefault(nid, {})[f"{plat}:{lang_tag}"] = text
    return out


def describe(path: Path) -> dict[str, str]:
    names = read_names(path)
    picked: dict[str, str] = {"file": path.name}
    for nid, label in NAME_IDS.items():
        variants = names.get(nid)
        if not variants:
            continue
        # 优先 Windows/en
        val = variants.get("3:en") or variants.get("3:zh") or next(iter(variants.values()))
        if label in ("typoFamily", "typoSubfamily") or label not in picked:
            picked[label] = val
    return picked


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    markdown = "--markdown" in sys.argv

    if args:
        files = [Path(a) for a in args]
    else:
        if not FONT_DIR.exists():
            print(f"[FAIL] 字体目录不存在: {FONT_DIR}", file=sys.stderr)
            return 1
        files = sorted(p for p in FONT_DIR.iterdir() if p.suffix.lower() in (".otf", ".ttf"))

    if not files:
        print("[FAIL] 没有找到字体文件", file=sys.stderr)
        return 1

    rows = []
    for f in files:
        try:
            rows.append(describe(f))
        except Exception as exc:  # noqa: BLE001
            rows.append({"file": f.name, "ERROR": str(exc)})

    if markdown:
        print("| 文件 | family (nameID 1) | typoFamily (nameID 16) | subfamily | WinUI 写法 |")
        print("| --- | --- | --- | --- | --- |")
        for r in rows:
            fam = r.get("family", "?")
            typo = r.get("typoFamily", "—")
            sub = r.get("subfamily", "—")
            print(f"| `{r['file']}` | `{fam}` | `{typo}` | `{sub}` | `ms-appx:///OreUI.WinUI/Assets/OreUI/Fonts/{r['file']}#{fam}` |")
    else:
        for r in rows:
            print(f"--- {r['file']}")
            for k in ("family", "typoFamily", "subfamily", "full", "postscript"):
                if k in r:
                    print(f"    {k:14} = {r[k]}")
            if "family" in r:
                print(f"    WinUI          = ms-appx:///OreUI.WinUI/Assets/OreUI/Fonts/{r['file']}#{r['family']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
