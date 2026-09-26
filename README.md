# repository

这些都是 AI 写的，我只测试！

本仓库是各类实验项目的**单仓多项目（monorepo）**归档。每个顶层目录是一个独立项目，
各自保留自己的构建方式与 `.gitignore`；仓库根只负责结构级规则（见 [`.gitignore`](.gitignore)）。

---

## 项目索引

| 目录 | 项目 | 技术栈 | 说明 |
| --- | --- | --- | --- |
| [`lanw/`](lanw/) | lanw | .NET 10 · WinUI 3 | Minecraft「脱盒」启动器 **Fantnel** 的 .NET 10 + WinUI 3 移植版。UI 全部自研，后端逻辑内嵌为进程内 HTTP 服务 |
| [`Oreui-Winui3/`](Oreui-Winui3/) | OreUI → WinUI 3 | .NET 10 · WinUI 3 · Windows App SDK | 把「我的世界 OreUI」设计语言从 HTML 重写为 WinUI 3 原生控件体系，渲染结果与 OreUI 调色板逐像素对齐 |
| [`Why-are-you-living-on-my-C-drive/`](Why-are-you-living-on-my-C-drive/) | Wyolm | .NET 10 · WinUI 3 | 把 C 盘上的大文件夹搬到其他盘，并在原位置留下目录联接（reparse point），让所有程序以为文件还在原地 |
| [`ai-mc-mod/`](ai-mc-mod/) | BBS Voice | Java 17 · Fabric · Gradle | BBS mod（Minecraft 1.20.4）的人声轨道插件：输入台词，在本地**离线**合成语音并贴到时间轴上 |

---

## 不入库的内容

以下目录是**外部参考源**，体积大且非自研代码，已在 `.gitignore` 中排除。
来源与版本记录在对应说明文件里，可按其中的命令重新获取：

| 路径 | 内容 | 来源记录 |
| --- | --- | --- |
| `_fantnel_ref/` | Fantnel 上游浅克隆，`lanw` 的长期参考源 | 按约定不入库 |
| `Oreui-Winui3/_source/` | Spectrollay-OreUI/OreUI 上游克隆 | 见 [`Oreui-Winui3/_source.REVISION`](Oreui-Winui3/_source.REVISION) |
| `ai-mc-mod/_ref/bbs-mod/` | mchorse/bbs-mod 上游克隆 | 见 [`ai-mc-mod/_ref/README.md`](ai-mc-mod/_ref/README.md) |

各项目的 `bin/`、`obj/`、`.gradle/`、`build/`、`artifacts/` 等**构建产物**同样不入库——
克隆后执行各自的构建命令重新生成即可。

---

## 各项目构建

见各目录下的 `README.md`。

---

## 许可

见 [`LICENSE`](LICENSE)。
