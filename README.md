# GGFramework Core

GGFramework 各模块共享基础类的**单一源**（single source of truth），用于消除五个仓库长期靠源码拷贝（vendoring）集成导致的**副本漂移**问题。

## Phase 1 范围

仅纳入**零第三方依赖**的基础类，可独立编译、不与任何消费方工程耦合：

| 类 | 命名空间 | 规范来源 | 说明 |
|---|---|---|---|
| `Singleton<T>` / `SingletonLifetime` | 全局 | GGUser 版 | 含非泛型 `SingletonLifetime.IsQuitting`，修复每个闭合泛型各持独立退出标志导致的幽灵对象问题 |
| `CoroutineUtil` | 全局 | 四仓库一致 | 协程封装，依赖 `Singleton<CoroutineUtil>` |
| `TaskSystem` / `GTask` | `GGFramework.GGTask` | GGNetwork 版 | 线程/任务管理 |

**不纳入 Core**（含第三方耦合；Phase 3 已定案：改为「副本对齐」而非拆为可选子程序集，详见 `GGFramework-Docs` 的 ADR-0003）：`GameDebugger`（耦合 CodeStage.AdvancedFPSCounter）、`ZipUtil`（耦合 SharpZipLib）、`UIAdaptor`/`LogAdaptor`（命名空间专属 + SimpleJson 依赖）。这些副本仍留在各消费方仓库，通过消除无意义漂移 + 修复 P0 缺陷来对齐，并保留有意的行为分叉（如 Demo 版 `GameDebugger` 不自动挂 FPS 计数器 / 不注册全局日志回调）。

## 目录结构

```
GGFramework-Core/            (仓库根即包根)
├── package.json
├── Runtime/
│   ├── GGFramework.Core.Runtime.asmdef
│   ├── Singleton.cs
│   ├── CoroutineUtil.cs
│   └── TaskSystem.cs
└── Tests/
    ├── GGFramework.Core.Tests.asmdef   (EditMode, UNITY_INCLUDE_TESTS)
    ├── CoreStructureTests.cs
    ├── SingletonLifetimeTests.cs
    └── GTaskTests.cs
```

## 存放位置与接入方式

本包为**独立仓库**（`GGFramework-Core`），仓库根即 UPM 包根，与任何消费方工程解耦，是纯加法、可逆的"安全基础层"。

消费方经 UPM git URL 引用（仓库根即包根，无需 `?path=`；建议锁定 tag 版本）。当前 `GGResource` / `GGNetwork` / `GGUI` / `GGUser` / `Demo` 五个工程均已接线并统一锁定 **v0.1.1**：

```json
"com.gonglei.ggframework.core": "https://github.com/gonglei007/GGFramework-Core.git#v0.1.1"
```

## 运行测试

包内 `Tests/` 是 UPM 包测试程序集，**默认不会被消费工程编译/运行**。要在某工程里执行 Core 的 EditMode 测试，须在该工程的 `Packages/manifest.json` 中把本包加入 `testables`：

```json
{
  "dependencies": {
    "com.gonglei.ggframework.core": "file:../../GGFramework-Core"
  },
  "testables": [ "com.gonglei.ggframework.core" ]
}
```

随后在 Test Runner 窗口或用命令行 `-runTests -testPlatform EditMode` 即可发现并运行全部用例（当前 10 个，已在 Unity 6000.3.13f1 验证全绿）。

> **Phase 2 关键约束**：`Singleton`/`CoroutineUtil` 位于全局命名空间。消费方一旦引用 Core，又同时保留本地 `Assembly-CSharp` 中的同名副本，会触发类型二义性编译错误。因此"引用 Core"必须与"删除本地副本"同批完成，不能分步。
