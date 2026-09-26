# `_ref` —— 外部参考源

本目录放第三方参考材料。其中 `bbs-mod/` 是上游仓库克隆，已在仓库根 `.gitignore` 中排除；
`pinyin-src.txt` 是小型数据文件，随仓库保留（见下文说明）。

---

## `bbs-mod/` —— BBS mod 上游源码克隆

供对照 BBS mod 的接口与轨道模型使用，**不入库**。

| 项 | 值 |
| --- | --- |
| upstream | https://github.com/mchorse/bbs-mod.git |
| branch | master |
| commit | 3cbfc79（2025-12-28，`Bump version (1.7.7)`） |

重新获取：

    git clone https://github.com/mchorse/bbs-mod.git _ref/bbs-mod

---

## `pinyin-src.txt` —— 汉字 → 拼音原始数据

`BBS Voice/tools/BuildPinyinTable.java` 的输入。生成的紧凑表已提交为
`BBS Voice/src/main/resources/assets/bbsvoice/pinyin.txt`，因此**构建并不需要本文件**；
保留它是为了让拼音表可以离线重新生成。

| 项 | 值 |
| --- | --- |
| 来源 | https://github.com/mozillazg/pinyin-data |
| 版本 | v0.15.0 |

重新生成拼音表（在 `BBS Voice/` 目录下执行）：

    java tools/BuildPinyinTable.java ../_ref/pinyin-src.txt src/main/resources/assets/bbsvoice/pinyin.txt

工具的参数约定为 `BuildPinyinTable <in> <out>`。
