# lanw — Fantnel 移植（.NET 10 + WinUI 3）

把 GitHub 上的 Minecraft「脱盒」启动器 **Fantnel**（`NirvanaTec/fantnel`）移植为
**.NET 10 + WinUI 3** 的桌面应用。内嵌后端逻辑、**无 HTTP 服务**，UI 全部自研、
与 Fantnel 拉开差异。项目按「全部重来、逐个功能移植」推进：先重建骨架，再逐个追加功能。

> Fantnel 参考源码浅克隆于 `D:\ku\traecode\_fantnel_ref`（工作区外），作长期参考源，
> 仅借鉴接口/逻辑层思路，代码自研。

## 环境

- Windows 10 17763 或更高
- .NET SDK 10.0（本机已有 10.0.400）

## 构建

```powershell
dotnet build lanw.slnx -c Debug
# WinUI App 在骨架通过 <Platform x64/> 映射构建：输出于
# src/App/Lanw.App/bin/x64/Debug/net10.0-windows10.0.19041.0/win-x64/
```

## 运行（Debug 非打包）

```powershell
dotnet run --project src/App/Lanw.App/Lanw.App.csproj -p:Platform=x64
```

## 测试

```powershell
dotnet test tests/Lanw.Core.Tests/Lanw.Core.Tests.csproj
```

## 目录结构

```
lanw/
├── lanw.slnx
├── Directory.Build.props          (.NET 10 / C# latest / nullable / implicit usings)
├── src/
│   ├── Core/Lanw.Core/            纯领域层（Models/Abstractions/Services，net10.0）
│   ├── Infrastructure/Lanw.Infrastructure/  基础设施（Cipher/Storage/Networking/Providers）
│   └── App/Lanw.App/               WinUI 3 桌面壳（net10.0-windows，风格自研）
└── tests/Lanw.Core.Tests/         xUnit 单测
```

## 移植进度

| 层 | Fantnel → lanw | 状态 |
|----|----------------|------|
| 领域基座 | `Nirvana.Common` → `Lanw.Core`（Entities/InfoManager/ErrorCode/Utils/Config） | ✅ 已移植（t3） |

> 命名约定：`NirvanaConfig`→`LanwConfig`、`PublicProgram`→`LanwProgram`；命名空间统一为 `Lanw.Core.*`；
> 依赖仅新增 `Serilog`；`Tools.CreateLinkDirectory` 中的 `Microsoft.VisualBasic.FileIO` 依赖改为本地
> `FileUtil.CopyDirectory` 递归复制实现（移除 VB 依赖）。

## 待移植功能（按序）

1. **账号登录 + 账号管理**（首个）：参照 Fantnel `AccountMessage`/`InfoManager`/
   `EntityAccount` 的编排与判重逻辑；登录类型 cookie/4399/4399com/163Email；
   安全存储改为 **DPAPI + 本地密文**（替代 Fantnel 明文 `account.json`）。
2. 服务器浏览 / 资源下载 / 代理 / 皮肤 / 名称（后续逐个追加）。
3. 启动模块（延后）：照搬/借鉴 `Nirvana.Game.Launcher`。
