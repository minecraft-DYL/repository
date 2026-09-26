#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从 spec/oreui-tokens.json 生成 WinUI 3 资源字典片段 xaml-src/00-tokens.xaml。

设计令牌只维护一份（spec/oreui-tokens.json），.NET 端的 Color / SolidColorBrush /
字号 / 尺寸全部由本脚本产出，避免手抄 CSS 造成漂移。

产物是「片段」而不是可直接编译的字典：最终 Themes/Generic.xaml 由
tools/gen_theme.py 把 xaml-src/ 下所有片段合并成单个自包含字典
（WinUI 的 MergedDictionaries 之间不共享资源查找作用域，必须合并）。

用法:
    python tools/gen_tokens.py            # 生成
    python tools/gen_tokens.py --check    # 只校验生成结果是否与磁盘一致（CI 用）
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPEC = os.path.join(ROOT, "spec", "oreui-tokens.json")
OUT = os.path.join(ROOT, "xaml-src", "00-tokens.xaml")

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!--
    OreUI 设计令牌 —— 自动生成，请勿手工编辑。
    生成器: tools/gen_tokens.py
    事实来源: spec/oreui-tokens.json
    Upstream: https://github.com/Spectrollay-OreUI/OreUI (MIT)
-->
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
"""


def pascal(name: str) -> str:
    """green-10 / deepBlue / topLeft / mc-ten -> Green10 / DeepBlue / TopLeft / McTen"""
    parts = re.split(r"[^0-9A-Za-z]+", name)
    out = []
    for p in parts:
        if not p:
            continue
        out.append(p[:1].upper() + p[1:])
    return "".join(out)


def css_to_xaml_color(value: str) -> str:
    """CSS #RRGGBB / #RRGGBBAA -> XAML #RRGGBB / #AARRGGBB"""
    v = value.strip()
    if v.startswith("#"):
        h = v[1:]
        if len(h) == 3:
            h = "".join(c * 2 for c in h)
        if len(h) == 6:
            return "#" + h.upper()
        if len(h) == 8:
            # CSS 是 RRGGBBAA，XAML 是 AARRGGBB
            return "#" + (h[6:8] + h[0:6]).upper()
        raise ValueError("无法解析颜色: %r" % value)
    raise ValueError("仅支持十六进制颜色, 收到: %r" % value)


class Emitter:
    def __init__(self) -> None:
        self.lines: list[str] = []

    def section(self, title: str) -> None:
        self.lines.append("")
        self.lines.append("    <!-- ==================== %s ==================== -->" % title)

    def color(self, key: str, css: str) -> str:
        xaml = css_to_xaml_color(css)
        self.lines.append('    <Color x:Key="%sColor">%s</Color>' % (key, xaml))
        self.lines.append(
            '    <SolidColorBrush x:Key="%sBrush" Color="%s" />' % (key, xaml)
        )
        return xaml

    def double(self, key: str, value) -> None:
        self.lines.append('    <x:Double x:Key="%s">%s</x:Double>' % (key, fmt_num(value)))

    def string(self, key: str, value: str) -> None:
        safe = (
            value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
        )
        self.lines.append('    <x:String x:Key="%s">%s</x:String>' % (key, safe))

    def font(self, key: str, value: str) -> None:
        # 必须是 FontFamily 元素：ThemeResource 不做类型转换，塞字符串会运行时报错
        safe = (
            value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
        )
        self.lines.append('    <FontFamily x:Key="%s">%s</FontFamily>' % (key, safe))

    def raw(self, text: str) -> None:
        self.lines.append("" if text == "" else "    " + text)


def font_uri(spec, key: str) -> str:
    """把 spec 的字体条目转成 WinUI 能用的 FontFamily 值。

    ⚠️ `#` 后面必须是字体**内部**的 family 名（OpenType name 表 nameID 1），
    不是 CSS @font-face 里起的逻辑名。实测：
        Minecraft-Ten.otf        -> "Minecraft Ten v2"   （不是 "Minecraft Ten"）
        Minecraft-Seven.otf      -> "Minecraft Seven v2"
        Minecraft-Five(-Bold).otf-> "Minecraft Five v2"
        NotoSans*.ttf            -> "Noto Sans"          （四个文件同名，靠字重区分）
    写错不会报错，只会静默回退到系统字体——所以改字体务必用
    tools/font_info.py 重新核对一遍。
    """
    entry = spec["typography"]["families"][key]
    if isinstance(entry, str):  # 兼容旧格式（只有逻辑名）
        return entry
    root = spec["typography"].get("fontAssetRoot", "Assets/OreUI/Fonts/")
    return "ms-appx:///%s%s#%s" % (root, entry["file"], entry["internal"])


def fmt_num(v) -> str:
    if isinstance(v, float):
        if v == int(v):
            return str(int(v))
        return ("%g" % v)
    return str(v)


# 令牌路径 -> 其中包含的「叶子」键名，这些键名不是颜色（不生成 Brush）
NON_COLOR_LEAVES = {"fontFamily", "fontSize", "width", "size"}


def is_color_value(v) -> bool:
    return isinstance(v, str) and bool(re.match(r"^#[0-9A-Fa-f]{3,8}$", v.strip()))


def resolve_ref(value, palette_lookup, semantic_lookup):
    """把 "gray.60" / "black" 之类的引用解析成具体的十六进制色值。"""
    if is_color_value(value):
        return value
    if isinstance(value, str) and "." in value:
        group, _, leaf = value.partition(".")
        if group in palette_lookup and leaf in palette_lookup[group]:
            return palette_lookup[group][leaf]
    for group in palette_lookup.values():
        if isinstance(group, dict) and value in group:
            return group[value]
    return None


def build_palette_lookup(spec):
    lookup = {}
    for group, entries in spec["palette"].items():
        flat = {}
        if isinstance(entries, dict):
            for k, v in entries.items():
                flat[k] = v
        lookup[group] = flat
    return lookup


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="校验磁盘上的生成结果是否最新")
    args = ap.parse_args()

    with open(SPEC, "r", encoding="utf-8") as f:
        spec = json.load(f)

    palette_lookup = build_palette_lookup(spec)
    em = Emitter()

    # ---------------- 原子色板 ----------------
    em.section("原子色板 (palette)")
    for group, entries in spec["palette"].items():
        if not isinstance(entries, dict):
            continue
        em.raw("")
        for leaf, value in entries.items():
            key = "OreUI%s%s" % (pascal(group), pascal(str(leaf)))
            if leaf == "white":
                key = "OreUI%sWhite" % pascal(group)
            if leaf == "black":
                key = "OreUI%sBlack" % pascal(group)
            em.color(key, value)

    # ---------------- 语义色 ----------------
    em.section("语义色 (semantic)")
    for group, entries in spec["semantic"].items():
        if group in ("dataviz", "datavizLines"):
            continue
        if not isinstance(entries, dict):
            continue
        em.raw("")
        for leaf, value in entries.items():
            resolved = resolve_ref(value, palette_lookup, None)
            if resolved is None:
                continue
            key = "OreUI%s%s" % (pascal(group), pascal(leaf))
            em.color(key, resolved)

    em.raw("")
    for i, value in enumerate(spec["semantic"]["dataviz"], start=1):
        em.color("OreUIDataviz%02d" % i, value)
    em.color("OreUIDatavizLines", spec["semantic"]["datavizLines"])
    em.color("OreUICanvasBackground", spec["semantic"]["background"] if "background" in spec["semantic"] else "#1E1E1F")

    # ---------------- 立体效果 ----------------
    em.section("立体效果 (effects: specular / bevel)")
    for leaf, value in spec["effects"].items():
        if leaf.startswith("$"):
            continue
        em.color("OreUI" + pascal(leaf), value)

    # ---------------- 字体 ----------------
    em.section("字体族 (typography.families)")
    for leaf in spec["typography"]["families"]:
        em.font("OreUIFont" + pascal(leaf), font_uri(spec, leaf))

    em.raw("")
    em.font("OreUIFontFamily", font_uri(spec, spec["typography"]["defaultFamily"]))
    em.font("OreUIFontTitleFamily", font_uri(spec, spec["typography"]["titleFamily"]))
    em.font("OreUIFontArticleTitleFamily", font_uri(spec, spec["typography"]["articleTitleFamily"]))
    em.font("OreUIFontBodyFamily", font_uri(spec, "notoBold"))
    em.font("OreUIFontRegularFamily", font_uri(spec, "noto"))

    em.raw("")
    for leaf, value in spec["typography"]["sizes"].items():
        em.double("OreUIFontSize" + pascal(leaf), value)

    # 行高：OreUI 的正文在 Web 上按 1.35 倍行距渲染
    em.raw("")
    em.double("OreUILineHeightFactor", 1.35)

    # ---------------- 组件尺寸 ----------------
    em.section("组件尺寸 (component metrics)")

    # 这些路径段只是分组名，不参与生成的 key（OreUIButtonWidthsSmall -> OreUIButtonWidthSmall）
    TRANSPARENT_SEGMENTS = {"widths", "sizes"}

    def walk(prefix: str, node) -> None:
        if isinstance(node, dict):
            for k, v in node.items():
                if str(k).startswith("$"):
                    continue
                seg = pascal(str(k))
                if str(k) in TRANSPARENT_SEGMENTS:
                    seg = ""
                walk(prefix + seg, v)
        elif isinstance(node, (int, float)) and not isinstance(node, bool):
            em.double(prefix, node)
        elif isinstance(node, list) and all(isinstance(x, (int, float)) for x in node) and node:
            # CSS 四值顺序: top right bottom left
            if len(node) == 4:
                for label, val in zip(("Top", "Right", "Bottom", "Left"), node):
                    em.double(prefix + label, val)
            else:
                for i, val in enumerate(node, start=1):
                    em.double(prefix + str(i), val)

    for group, node in spec["component"].items():
        if group == "sounds":
            continue
        em.raw("")
        walk("OreUI" + pascal(group), node)

    em.lines.append("")
    em.lines.append("</ResourceDictionary>")
    em.lines.append("")

    text = HEADER + "\n".join(em.lines).lstrip("\n")

    if args.check:
        try:
            with open(OUT, "r", encoding="utf-8-sig") as f:
                current = f.read()
        except FileNotFoundError:
            print("缺失: %s" % OUT, file=sys.stderr)
            return 1
        if current != text:
            print("过期: %s 与 spec 不一致，请运行 tools/gen_tokens.py" % OUT, file=sys.stderr)
            return 1
        print("OK: %s 已是最新" % OUT)
        return 0

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    # 必须带 UTF-8 BOM：XAML 编译器在没有 BOM 时会按系统 ANSI 代码页读取，中文注释与
    # 字体族名会变成乱码。
    with open(OUT, "w", encoding="utf-8-sig", newline="\n") as f:
        f.write(text)
    print("已生成 %s (%d 行)" % (OUT, text.count("\n")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
