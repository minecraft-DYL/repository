#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
确保 .cs / .xaml 源文件带 UTF-8 BOM。

原因：XAML 编译器（Microsoft.UI.Xaml.Markup.Compiler）在没有 BOM 时会用系统 ANSI
代码页读取文件，中文注释和字体族名会乱码；Roslyn 虽然默认按 UTF-8 解析，但统一加
BOM 更稳妥。

用法:
    python tools/ensure_bom.py                 # 处理 dotnet/ 下所有 .cs/.xaml
    python tools/ensure_bom.py --check         # 只报告缺失 BOM 的文件（CI 用）
"""
from __future__ import annotations

import argparse
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCAN_DIR = os.path.join(ROOT, "dotnet")
EXTS = (".cs", ".xaml")
SKIP_DIRS = {"obj", "bin", ".vs"}


def iter_sources():
    for base, dirs, files in os.walk(SCAN_DIR):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            if name.endswith(EXTS):
                yield os.path.join(base, name)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    missing = []
    fixed = []
    for path in iter_sources():
        with open(path, "rb") as f:
            raw = f.read()
        if raw.startswith(b"\xef\xbb\xbf"):
            continue
        # 只有含非 ASCII 的文件才必须加 BOM
        try:
            raw.decode("ascii")
            continue
        except UnicodeDecodeError:
            pass
        missing.append(path)
        if not args.check:
            try:
                text = raw.decode("utf-8")
            except UnicodeDecodeError as exc:
                print("跳过（非 UTF-8）: %s (%s)" % (path, exc), file=sys.stderr)
                continue
            with open(path, "wb") as f:
                f.write(b"\xef\xbb\xbf" + text.encode("utf-8"))
            fixed.append(path)

    if args.check and missing:
        for p in missing:
            print("缺少 UTF-8 BOM: %s" % os.path.relpath(p, ROOT), file=sys.stderr)
        return 1

    for p in fixed:
        print("已补 BOM: %s" % os.path.relpath(p, ROOT))
    if not fixed:
        print("无需修复：所有含非 ASCII 的 .cs/.xaml 都已有 UTF-8 BOM")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
