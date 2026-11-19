# Changelog

## [0.3.0] - 2026-11-19

- 新增主线程派发器 `MainThreadDispatcher`、跨设备可写路径解析 `PersistentPathResolver`、格式化与编码工具 `FormatUtil`、网络工具 `NetUtil`。
- 保留 v0.2.0 的对象池、状态机、事件总线，以及既有单例、协程和任务 API。
- 修复快速 `GTask` 完成回调丢失的竞态；任务线程以后台线程运行，释放操作支持尚未运行或已结束的线程。
- `TaskSystem.Init` 幂等，宿主销毁时退出后台队列循环，等待中的队列避免空转。
- 超时后不再销毁仍在运行的 Task，允许后续排队任务继续执行；超时不等于取消工作函数。
- 包含 66 项已通过 Unity 6000.3.13f1 验证的 EditMode 测试。
- 发布固定标签 `v0.3.0`，支持 Demo 通过公开 GitHub UPM 依赖独立克隆和运行。
