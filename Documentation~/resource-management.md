# 资源缓存与请求准入（0.4.0-preview.1）

这两个基础类位于 `GGFramework.Core` 命名空间，不依赖 Unity 或第三方库，也不替代 GGResource 的 AssetBundle / Simulation 后端。它们负责内存权重与请求所有权；路径解析、加载、释放 Unity 对象、取消 I/O 和绑定界面都由宿主完成。

当前稳定标签仍为 `v0.3.0`。本预览版提交到 `main` 不等于已发布版本标签；试用时固定包含本次修改的完整 Git 提交，或在开发工程中使用本地路径，并记录源码 / 包摘要。不要仅凭包版本号推断测试结果或让生产工程跟随浮动 `main`。

## 线程与生命周期

所有操作（包括读取状态）均由同一宿主线程调用。异步加载的完成回调先派发回该线程；Unity 工程可以使用 `MainThreadDispatcher`。类本身不加锁，也不调度线程。

缓存与队列相互独立：队列完成并不会自动写缓存，清缓存不会自动取消请求。宿主应在切换场景 / 页面时先解除旧界面对资源的引用、使旧请求失效，再建立新的使用范围。不要在旧消费者仍使用资源时调用 `UnpinAll` 后驱逐，或调用 `Clear`。

## BudgetedResourceCache<TKey, TValue>

| API / 状态 | 契约 |
| --- | --- |
| 构造函数 | `release` 必填；预算为负时按 0 处理；可传键比较器。字符串资源路径建议显式使用 `StringComparer.Ordinal`。 |
| `Add(key, value, bytes, pinned = true)` | 新增一个键的所有权；默认保护。字节权重必须非负，总权重溢出或重复 / 空键会在转移前报错。不会自动驱逐，宿主完成结果绑定后显式 trim。 |
| `TryGet` | 命中会更新 LRU 顺序并保护该条目；重复读取只计一次保护权重。未命中返回 `false` 与 `default(TValue)`。这是有副作用的访问。 |
| `UnpinAll` | 结束整个使用范围的保护；不释放、不 trim。不提供逐消费者引用计数或多个重叠范围。 |
| `TrimToBudget` / `Trim(targetBytes)` | 按 LRU 移除未保护条目，直到总权重达到目标或没有可驱逐条目。负目标按 0 处理；`Trim` 不修改配置预算。 |
| `BudgetBytes` | 修改时立即 trim；总预算包含受保护条目，保护权重大于预算时允许超预算。 |
| `Clear` | 强制移除所有条目，包括受保护条目；仅用于消费者已停止使用后的完整拆除。 |
| `TotalBytes` / `PinnedBytes` | 宿主声明的总权重 / 保护权重，不是进程、GPU 或实际分配内存测量。 |
| `Evictions` | 实例生命周期内被移除的条目数，包括 `Clear`，不是释放成功次数。 |

每个接受的条目对应一次 `release(value)`。相同对象放到多个键时，宿主必须为每个条目提供独立所有权（例如自行 retain），否则可能重复释放。权重为 0 的条目也持有所有权；当总权重已达目标时 trim 不会为了清空它们继续驱逐，应按需 `Clear`。

驱逐 / 清理先移除所有目标并更新账本，再依次调用释放回调。一个回调失败不阻断其他释放，最终抛出包含所有错误的 `AggregateException`；这些条目已经脱离缓存，之后不会自动重试释放。释放失败的补救由宿主负责。释放回调可以读取计数，不能调用 `Add`、`TryGet`、`UnpinAll`、trim、`Clear` 或修改预算；重入会抛出 `InvalidOperationException`，计入聚合错误。

```csharp
using System;
using System.Collections.Generic;
using GGFramework.Core;

public static class ResourceCacheExample
{
    public static void Run()
    {
        var released = new List<string>();
        var cache = new BudgetedResourceCache<string, string>(
            10, released.Add, StringComparer.Ordinal);
        cache.Add("old", "old asset", 6);
        cache.Add("current", "current asset", 6);
        cache.TrimToBudget(); // 两者都受保护，允许 12 > 10。

        // 宿主先解除旧消费者；新范围继续使用 current。
        cache.UnpinAll();
        string value;
        cache.TryGet("current", out value);
        cache.TrimToBudget(); // 释放 old，留下受保护的 current。

        // 宿主解除最后一个消费者之后再完整清理。
        cache.Clear();
    }
}
```

## ResourceRequestQueue<TKey, TRequest>

| API / 状态 | 契约 |
| --- | --- |
| 构造函数 / `MaxConcurrency` | 并发上限至少 1；可传键比较器。降低上限不终止已准入的工作，升高上限后需显式 `Pump`。 |
| `GetOrCreate(key, create)` | 已排队或运行的同键请求返回同一个 `Lease`，不调用工厂（此时允许 `create` 为 null）。新请求的工厂必填且返回值非 null；工厂失败不注册租约。 |
| 请求工厂 | 只构造请求，可以查询状态 / `IsCurrent`；不能重入 `GetOrCreate`、`Pump`、`Complete`、`CancelAll` 或修改上限。重入在改变状态前抛出 `InvalidOperationException`，异常后可重试。 |
| `Pump(start)` | 按 FIFO 准入直到达到上限；同步完成可以继续准入。`start` 中的嵌套 `Pump` 忽略，由外层继续；工厂中的重入则报错。 |
| `IsCurrent(lease)` | 判断是否仍为本实例当前租约，包括排队态；不是“正在运行”的判断。其他队列、已完成 / 取消租约和 null 均为 false。 |
| `Complete(lease)` | 仅接受当前运行租约并释放一次逻辑槽位；重复、排队、过时或外来完成返回 false。不会自动 `Pump`、绑定结果或释放请求。 |
| `CancelAll` | 先使全部排队 / 运行租约失效并清零槽位，再返回租约数组；返回顺序未定义。宿主随后终止真实 I/O。 |
| `PendingCount` | 排队与运行的请求总数。 |
| `ActiveCount` / `PeakActiveCount` | 当前逻辑准入量 / 实例生命周期内峰值；取消不代表物理 I/O 已经停止。 |
| `CancelledCount` | 累计取消的排队与运行租约数；正常完成、启动失败不计入。 |

异步结果返回时，应先用 `IsCurrent(lease)` 检查归属，再绑定到当前界面或缓存。旧结果不得覆盖同键的新请求；宿主负责释放不再需要的旧结果。完成后调用 `Complete`，再显式 `Pump` 启动后续工作。取消时先 `CancelAll`，再逐项 abort，确保 abort 触发的同步回调已经过时。

`start` 抛错时会解除其仍然有效的运行租约，再将原异常抛给宿主；本次 pump 停止，后续请求保留，宿主可另行再次 pump。如果已经发起真实 I/O，宿主必须先确保其终止 / 回调不会错误绑定。队列不销毁 `Lease.Value`，也不缓存已完成租约。

```csharp
using GGFramework.Core;

public static class ResourceQueueExample
{
    private sealed class Request { public string Path; }

    public static bool Run()
    {
        var queue = new ResourceRequestQueue<string, Request>(1);
        var old = queue.GetOrCreate("tile", () => new Request { Path = "tile" });
        queue.Pump(lease => { /* 宿主在这里开始 I/O，完成回调回到同一线程。 */ });

        var cancelled = queue.CancelAll(); // 先失效，再由宿主逐项取消 cancelled 的 I/O。
        var next = queue.GetOrCreate("tile", () => new Request { Path = "tile" });
        queue.Pump(lease => { /* 新范围的加载。 */ });

        bool staleIgnored = !queue.IsCurrent(old) && !queue.Complete(old);
        return staleIgnored && queue.Complete(next) && queue.ActiveCount == 0;
    }
}
```

## 验证与接入边界

- 本次验证结果、环境和复现命令见[预览版验证记录](preview-0.4.0-validation.md)。
- [共享契约场景](../Tests/ResourceManagementScenarios.cs)由 NUnit 和 [SDK 独立入口](../Tests~/ResourceContracts/README.md)执行，覆盖预算 / LRU、保护范围、失败回收、回调重入、FIFO、去重、并发调整、取消替换及陈旧 / 外来完成。
- Unity 测试必须在消费工程中配置 `testables` 并指向待测源码；使用稳定 `#v0.3.0` 只能测试旧发布包。[README 的测试说明](../README.md#运行测试)给出切换方式。
- 契约测试不模拟 AssetBundle、纹理销毁、网络中断或场景加载。宿主仍需验证字节估算、I/O abort、消费者解除引用与主线程派发。
