# GGFramework Core

GGFramework 的共享基础库，以独立 UPM 包维护单例、协程与任务、对象池、状态机、事件总线、主线程派发及通用工具；零第三方依赖。

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-222222?logo=unity)](https://github.com/gonglei007/GGFramework-Core/blob/v0.3.0/package.json) [![Core stable v0.3.0](https://img.shields.io/badge/Core_stable-v0.3.0-2563eb)](https://github.com/gonglei007/GGFramework-Core/tree/v0.3.0) [![Branch main](https://img.shields.io/badge/Branch-main-64748b)](https://github.com/gonglei007/GGFramework-Core/tree/main)

## 项目导航

[Demo 体验](https://github.com/gonglei007/GGFramework-Demo) · [架构文档](https://github.com/gonglei007/GGFramework-Docs) · **Core** · [GGResource](https://github.com/gonglei007/GGFramework-GGResource) · [GGNetwork](https://github.com/gonglei007/GGFramework-GGNetwork) · [GGUI](https://github.com/gonglei007/GGFramework-GGUI) · [GGUser](https://github.com/gonglei007/GGFramework-GGUser)

> **先体验整体框架**：[打开独立 Demo](https://github.com/gonglei007/GGFramework-Demo#快速开始)，体验资源、HTTP、UI、用户 SDK 与 Lua 的集成流程。[操作指南](https://github.com/gonglei007/GGFramework-Demo/blob/main/documents/showcase-guide.md) · [验收记录](https://github.com/gonglei007/GGFramework-Demo/blob/main/documents/showcase-verification.md)。

## 模块关系

Core 是五个 Unity 工程共同引用的基础层。各工程按自己的 manifest 固定版本；当前稳定发布为 **v0.3.0**，Demo 使用该版本，其他模块的实际锁定版本见对应模块的 README。模块业务代码和第三方适配实现由各自仓库维护。

完整依赖图、工程锁定版本和源码副本边界由[架构文档中心](https://github.com/gonglei007/GGFramework-Docs#模块关系)统一维护。

## 快速开始

1. 安装 Git，并确保 Unity Hub 启动的编辑器能找到 Git。Core 包声明最低 Unity **2021.3**，统一体验工程使用 **6000.3.13f1**。
2. 将下列依赖条目合并到现有 Unity 工程的 `Packages/manifest.json`，等待 Package Manager 完成解析：

```json
{
  "dependencies": {
    "com.gonglei.ggframework.core": "https://github.com/gonglei007/GGFramework-Core.git#v0.3.0"
  }
}
```

3. 创建 `CoreHello.cs` 并挂到场景对象，播放后 Console 输出 `1`：

```csharp
using UnityEngine;
using GGFramework.Core;

public sealed class CoreHello : MonoBehaviour
{
    private void Start()
    {
        var events = new EventBus();
        events.Subscribe<int>(value => Debug.Log(value));
        events.Publish(1);
    }
}
```

4. 通过[独立 Demo](https://github.com/gonglei007/GGFramework-Demo#快速开始)运行对象池、状态机和主线程回调示例；包测试的开启方式见[运行测试](#运行测试)。[版本变更](CHANGELOG.md)记录各次发布；稳定接入示例固定使用已发布标签。

## Phase 1 范围

仅纳入**零第三方依赖**的基础类，可独立编译、不与任何消费方工程耦合：

| 类 | 命名空间 | 规范来源 | 说明 |
|---|---|---|---|
| `Singleton<T>` / `SingletonLifetime` | 全局 | GGUser 版 | 含非泛型 `SingletonLifetime.IsQuitting`，修复每个闭合泛型各持独立退出标志导致的幽灵对象问题 |
| `CoroutineUtil` | 全局 | 四仓库一致 | 协程封装，依赖 `Singleton<CoroutineUtil>` |
| `TaskSystem` / `GTask` | `GGFramework.GGTask` | GGNetwork 版 | 线程/任务管理 |

**不纳入 Core**（含第三方耦合；Phase 3 已定案：改为「副本对齐」而非拆为可选子程序集，详见 `GGFramework-Docs` 的 ADR-0003）：`GameDebugger`（耦合 CodeStage.AdvancedFPSCounter）、`ZipUtil`（耦合 SharpZipLib）、`UIAdaptor`/`LogAdaptor`（命名空间专属 + SimpleJson 依赖）。这些副本仍留在各消费方仓库，通过消除无意义漂移 + 修复 P0 缺陷来对齐，并保留有意的行为分叉（如 Demo 版 `GameDebugger` 不自动挂 FPS 计数器 / 不注册全局日志回调）。

## v0.2.0 增补：对象池 / 状态机 / 事件总线

面向贴纸游戏 Unity 移植暴露出的通用需求（详见 `GGFramework-Docs` 的 `roadmap/sticker-game-unity-porting.md` §4.1.3），在 Core 增补三个**零第三方依赖**的基础模块，统一位于 `GGFramework.Core` 命名空间（避开 `UnityEngine.Pool.ObjectPool<T>`、`MonsterLove.StateMachine` 等类型的二义性）：

| 类 | 文件 | 说明 |
|---|---|---|
| `Pool<T>` | `Runtime/ObjectPool.cs` | 泛型对象池（纯 POCO）：工厂委托创建，取出/归还触发 `onGet`/`onRelease` 重置回调，支持预热与 `maxSize` |
| `GameObjectPool` | `Runtime/ObjectPool.cs` | GameObject 专用池：基于预制体克隆，取出 `SetActive(true)`、归还挂回池根；EditMode 下用 `DestroyImmediate` 销毁 |
| `StateMachine<TState>` | `Runtime/StateMachine.cs` | 枚举驱动的轻量 FSM：`Configure` 注册进入/退出/更新回调、`ChangeState`、`Update(dt)` 转发、转移守卫；对标替代 GGUser 借用的第三方 MonsterLove |
| `EventBus` | `Runtime/EventBus.cs` | 类型安全发布/订阅：`Subscribe<T>`/`Unsubscribe<T>`/`Publish<T>`，快照派发（订阅者去重、派发中改订阅安全、单处理器异常隔离）；实例化设计便于测试，可由消费方用 `Singleton<T>` 包装为全局单点 |

> v0.2.0 为**纯加法**变更，不改动既有 `Singleton`/`CoroutineUtil`/`TaskSystem`，因此仍锁定 `#v0.1.1` 的消费方不受影响；需要三件套的工程可将 UPM 引用升到 `#v0.2.0`。

## v0.3.0 增补：主线程派发 / 平台路径 / 通用工具

从业务项目（WJSanGuo2）提炼出的三项**零第三方依赖**通用基础设施，填补 Core 现有能力空白，统一位于 `GGFramework.Core` 命名空间：

| 类 | 文件 | 说明 |
|---|---|---|
| `MainThreadDispatcher` | `Runtime/MainThreadDispatcher.cs` | 主线程派发器：静态 `ConcurrentQueue<Action>` + MonoBehaviour 每帧泵，`Post`（在主线程立即执行、否则入队）/`Enqueue`/`ProcessQueue`（异常隔离）/`IsMainThread`/`PendingCount`；派发 API 全静态，EditMode 测试无需 GameObject 即可驱动。修正原版仅靠静态字段初始化器捕获主线程 ID 的隐患 |
| `PersistentPathResolver` | `Runtime/PersistentPathResolver.cs` | 跨设备可写持久化路径解析：`IsDirectoryWritable` 写探针、Android 分区存储下 `Internal`/`External` 回退、best-first 的 `Candidates`/`Best`、`FileExists`；编辑器与非 Android 回退 `Application.persistentDataPath` |
| `FormatUtil` | `Runtime/FormatUtil.cs` | 通用格式化/编码纯函数：`DataSize`（字节→"1.5 MB"）、`UnixTimestampMs`、`Md5Hex`（小写 32 位十六进制） |
| `NetUtil` | `Runtime/NetUtil.cs` | 通用网络纯函数：`HostFromUrl`、`ResolveHostAddress`（带超时的同步 DNS）、`Sign`（查询参数按 key 排序后 MD5 签名）；已剔除原版的 MonoBehaviour 基类、SimpleJson 依赖与业务/编辑器专属方法 |

> v0.3.0 新增上述基础设施，并包含下文的任务系统兼容性修复，保留既有公开 API。已发布固定标签 `v0.3.0`；Demo 通过该标签接入，锁定旧版本的其他消费方不受影响。完整变更见 [CHANGELOG](CHANGELOG.md)。

## 目录结构

```
GGFramework-Core/            (仓库根即包根)
├── package.json
├── Runtime/
│   ├── GGFramework.Core.Runtime.asmdef
│   ├── Singleton.cs
│   ├── CoroutineUtil.cs
│   ├── TaskSystem.cs
│   ├── ObjectPool.cs        (Pool<T> / GameObjectPool)
│   ├── StateMachine.cs      (StateMachine<TState>)
│   ├── EventBus.cs          (EventBus)
│   ├── MainThreadDispatcher.cs   (MainThreadDispatcher)
│   ├── PersistentPathResolver.cs (PersistentPathResolver)
│   ├── FormatUtil.cs        (FormatUtil)
│   ├── NetUtil.cs           (NetUtil)
│   ├── BudgetedResourceCache.cs (加权 LRU 与范围保护，预览版)
│   └── ResourceRequestQueue.cs  (请求准入与取消租约，预览版)
├── Tests/
│   ├── GGFramework.Core.Tests.asmdef   (EditMode, UNITY_INCLUDE_TESTS)
│   ├── CoreStructureTests.cs
│   ├── SingletonLifetimeTests.cs
│   ├── GTaskTests.cs
│   ├── TaskSystemTests.cs
│   ├── ObjectPoolTests.cs
│   ├── StateMachineTests.cs
│   ├── EventBusTests.cs
│   ├── MainThreadDispatcherTests.cs
│   ├── PersistentPathResolverTests.cs
│   ├── FormatUtilTests.cs
│   ├── NetUtilTests.cs
│   ├── ResourceManagementScenarios.cs (NUnit / SDK 共享契约)
│   └── ResourceManagementTests.cs
├── Tests~/ResourceContracts/ (独立 SDK 测试入口，Unity 忽略)
└── Documentation~/resource-management.md (资源管理接入契约)
```

## 存放位置与接入方式

本包为**独立仓库**（`GGFramework-Core`），仓库根即 UPM 包根，与任何消费方工程解耦，是纯加法、可逆的"安全基础层"。

消费方经 UPM git URL 引用（仓库根即包根，无需 `?path=`）。Demo 固定使用已发布的 **v0.3.0**；开发者只需安装 Git 并克隆 Demo，Unity Package Manager 会自动获取 Core，无需同级检出本仓库。其他模块维持各自 manifest 中的版本声明。

```json
"com.gonglei.ggframework.core": "https://github.com/gonglei007/GGFramework-Core.git#v0.3.0"
```

## 运行测试

包内 `Tests/` 是 UPM 包测试程序集，**默认不会被消费工程编译/运行**。要在某工程里执行 Core 的 EditMode 测试，须在该工程的 `Packages/manifest.json` 中把本包加入 `testables`：

```json
{
  "dependencies": {
    "com.gonglei.ggframework.core": "https://github.com/gonglei007/GGFramework-Core.git#v0.3.0"
  },
  "testables": [ "com.gonglei.ggframework.core" ]
}
```

随后在 Test Runner 窗口或用命令行 `-runTests -testPlatform EditMode` 运行包测试。固定 `v0.3.0` 包有 66 个测试；本地 `0.4.0-preview.1` 在此基础上增加 38 个资源契约用例。测试预览源码时必须切换到本地路径或包含预览修改的完整 Git 提交，不能用稳定标签的结果代替。

Demo 已启用 `testables`，可直接用它运行固定版本的包测试。需要修改 Core 源码时，开发者可以自行将依赖临时改为本地路径；对于 `GGFramework/<消费仓库>/<Unity工程>/Packages/manifest.json` 的同级仓库布局，该路径为 `file:../../../GGFramework-Core`（Unity 从 `Packages` 目录解析），默认发布配置不使用本地路径。

资源缓存 / 队列也提供不依赖 Unity 的独立入口，需要 .NET 10 SDK。在 Core 仓库根目录运行：

```powershell
dotnet run --project "Tests~/ResourceContracts/ResourceContracts.csproj" --configuration Release
```

此入口只运行两项新增基础设施的 38 个契约场景，不替代全量 Unity 回归。[独立测试说明](Tests~/ResourceContracts/README.md)列出环境与退出码，[资源接入文档](Documentation~/resource-management.md)说明宿主边界。

### Demo 集成修复

任务系统现在会在宿主销毁时退出后台队列循环，`Init` 重复调用不会创建多个队列泵；队列忙时短暂休眠，避免空转。超时的任务可以继续结束，系统不会对仍在运行的 Task 调用 `Dispose`。`GTask` 在启动线程前登记完成回调，修复快速任务丢失回调的竞态。超时只允许队列继续推进，不会强制取消工作函数；工作函数应自行处理取消或终止。

> **Phase 2 关键约束**：`Singleton`/`CoroutineUtil` 位于全局命名空间。消费方一旦引用 Core，又同时保留本地 `Assembly-CSharp` 中的同名副本，会触发类型二义性编译错误。因此"引用 Core"必须与"删除本地副本"同批完成，不能分步。

## 0.4.0-preview.1：资源缓存与请求准入（预览版）

`BudgetedResourceCache<TKey, TValue>` 提供加权 LRU、范围保护和预算回收；`ResourceRequestQueue<TKey, TRequest>` 提供键去重、FIFO 并发准入及取消租约。两者都是单线程纯托管基础设施，实际 I/O、Unity 对象释放和界面绑定由宿主提供。

缓存保护是整个使用范围的布尔标记，不是多消费者引用计数；队列取消只使租约失效，宿主还需终止 I/O 并释放过时结果。释放回调和请求工厂的重入有明确限制。完整 API、生命周期顺序与可运行示例见[资源管理接入文档](Documentation~/resource-management.md)。

稳定发布仍为 `v0.3.0`；预览代码进入 `main` 不等于发布版本标签。采用项目须固定完整来源和包摘要，不能把预览版号当作验证结果。版本范围见 [CHANGELOG](CHANGELOG.md)，实际环境与结果见[预览版验证记录](Documentation~/preview-0.4.0-validation.md)。
