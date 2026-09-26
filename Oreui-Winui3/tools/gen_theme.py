#!/usr/bin/env python3
"""把 xaml-src/ 下的 XAML 片段合并成单个自包含的 Themes/Generic.xaml。

为什么必须合并成单文件：
  WinUI 的 ResourceDictionary.MergedDictionaries 之间 **不共享资源查找作用域**。
  样式写在 30-surfaces.xaml 里、令牌写在 00-tokens.xaml 里时，样式里的
  {ThemeResource OreUIFontSizePageTitle} 在运行期会抛
  "Cannot find a Resource with the Name/Key ..."。
  把令牌和样式放进同一个字典即可让查找作用域合一，
  同时也避免嵌套 <ResourceDictionary Source="..."> 的解析开销与坑。

用法：
  python tools/gen_theme.py            # 生成
  python tools/gen_theme.py --check    # 只校验是否与磁盘一致（CI 用）
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
SRC_DIR = REPO / "xaml-src"
OUT = REPO / "dotnet" / "src" / "OreUI.WinUI" / "Themes" / "Generic.xaml"

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!--
    ⚠️ 本文件由 tools/gen_theme.py 自动生成，请勿手工修改。
    源片段位于仓库根的 xaml-src/ 目录，修改后重新运行：
        python tools/gen_tokens.py && python tools/gen_theme.py
-->
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:OreUI.WinUI">
"""

FOOTER = "\n</ResourceDictionary>\n"

_OPEN_TAG = re.compile(r"<ResourceDictionary\b[^>]*>", re.DOTALL)
_XML_DECL = re.compile(r"^\s*<\?xml[^>]*\?>\s*", re.DOTALL)
_CLOSE_TAG = re.compile(r"</ResourceDictionary>\s*$")


def strip_wrapper(text: str) -> str:
    """去掉片段自身的 <?xml?> 声明和 <ResourceDictionary> 外壳，只留内容。"""
    text = _XML_DECL.sub("", text)
    open_match = _OPEN_TAG.search(text)
    if open_match is None:
        raise ValueError("片段缺少 <ResourceDictionary> 根元素")
    text = text[open_match.end():]
    text = _CLOSE_TAG.sub("", text)
    return text.strip("\n")


def build() -> str:
    parts = sorted(SRC_DIR.glob("*.xaml"))
    if not parts:
        raise SystemExit(f"未在 {SRC_DIR} 找到任何 .xaml 片段")

    chunks = [HEADER]
    for part in parts:
        body = strip_wrapper(part.read_text(encoding="utf-8-sig"))
        label = part.name
        chunks.append(f"\n    <!-- ==================== {label} ==================== -->\n")
        chunks.append(body)
        chunks.append("\n")
    chunks.append(FOOTER)
    return "".join(chunks)


def main() -> int:
    text = build()
    check = "--check" in sys.argv
    if check:
        current = OUT.read_text(encoding="utf-8-sig") if OUT.exists() else ""
        if current != text:
            print(f"[FAIL] {OUT} 与 xaml-src/ 不一致，请运行 python tools/gen_theme.py")
            return 1
        print(f"[OK] {OUT} 已是最新")
        return 0

    OUT.parent.mkdir(parents=True, exist_ok=True)
    # XAML 编译器在缺少 BOM 时会按 ANSI 读取，含中文注释必须写 utf-8-sig
    with open(OUT, "w", encoding="utf-8-sig", newline="\n") as f:
        f.write(text)
    print(f"[OK] 已生成 {OUT.relative_to(REPO)}（{text.count(chr(10))} 行）")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
