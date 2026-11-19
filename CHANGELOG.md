# Changelog

## [0.4.0-preview.1] - 2026-11-20

- 新增纯托管 `BudgetedResourceCache<TKey, TValue>`：加权 LRU、使用范围保护、显式 trim / clear、清理错误聚合与释放回调重入保护。
- 新增 `ResourceRequestQueue<TKey, TRequest>`：同键排队 / 运行去重、FIFO 并发准入、租约身份检查、同步完成与范围取消；请求工厂禁止重入修改队列，失败不注册请求。
- 补齐 38 个共享资源契约场景，由 NUnit EditMode 与无第三方依赖的 .NET 10 独立入口执行，保留既有 Core API。
- 补充资源管理 API、所有权 / 取消边界、可运行示例及验证方式。预算是宿主声明的权重，取消只解除逻辑租约；真实 I/O、消费者引用与对象释放仍由宿主负责。
- 此版本为预览包；稳定标签与 Demo 的固定依赖仍为 `v0.3.0`，本次不发布新标签或更改消费工程依赖。

## [0.3.0] - 2026-11-19

- 新增主线程派发器 `MainThreadDispatcher`、跨设备可写路径解析 `PersistentPathResolver`、格式化与编码工具 `FormatUtil`、网络工具 `NetUtil`。
- 保留 v0.2.0 的对象池、状态机、事件总线，以及既有单例、协程和任务 API。
- 修复快速 `GTask` 完成回调丢失的竞态；任务线程以后台线程运行，释放操作支持尚未运行或已结束的线程。
- `TaskSystem.Init` 幂等，宿主销毁时退出后台队列循环，等待中的队列避免空转。
- 超时后不再销毁仍在运行的 Task，允许后续排队任务继续执行；超时不等于取消工作函数。
- 包含 66 项已通过 Unity 6000.3.13f1 验证的 EditMode 测试。
- 发布固定标签 `v0.3.0`，支持 Demo 通过公开 GitHub UPM 依赖独立克隆和运行。
