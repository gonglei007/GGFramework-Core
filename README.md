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

> v0.3.0 为**纯加法**变更，不改动既有类，锁定 `#v0.1.1` 的消费方不受影响；需要这批工具的工程可将 UPM 引用升到 `#v0.3.0`。本次不接线任何消费方。

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
│   └── NetUtil.cs           (NetUtil)
└── Tests/
    ├── GGFramework.Core.Tests.asmdef   (EditMode, UNITY_INCLUDE_TESTS)
    ├── CoreStructureTests.cs
    ├── SingletonLifetimeTests.cs
    ├── GTaskTests.cs
    ├── ObjectPoolTests.cs
    ├── StateMachineTests.cs
    ├── EventBusTests.cs
    ├── MainThreadDispatcherTests.cs
    ├── PersistentPathResolverTests.cs
    ├── FormatUtilTests.cs
    └── NetUtilTests.cs
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

随后在 Test Runner 窗口或用命令行 `-runTests -testPlatform EditMode` 即可发现并运行全部用例（当前 64 个，已在 Unity 6000.3.13f1 验证全绿）。

> **Phase 2 关键约束**：`Singleton`/`CoroutineUtil` 位于全局命名空间。消费方一旦引用 Core，又同时保留本地 `Assembly-CSharp` 中的同名副本，会触发类型二义性编译错误。因此"引用 Core"必须与"删除本地副本"同批完成，不能分步。
