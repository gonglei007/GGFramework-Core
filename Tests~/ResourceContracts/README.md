# 资源契约独立测试

要求安装 **.NET 10 SDK**。从 Core 仓库根目录运行：

```powershell
dotnet run --project "Tests~/ResourceContracts/ResourceContracts.csproj" --configuration Release
```

程序直接编译两份 Runtime 源码与 `Tests/ResourceManagementScenarios.cs`，使用 C# 9 语法上限。没有 NuGet 第三方依赖；此目录的 `NuGet.Config` 清空包源，SDK 自带的框架引用即可还原。失败返回退出码 1，成功返回 0；每个案例都创建独立状态。

这些场景与 Unity EditMode 的 `ResourceManagementTests` 完全共用，但此入口**只验证资源缓存和请求队列**，不替代整个 Core 的 Unity 回归测试。Unity 会忽略 `Tests~` / `Documentation~` 目录；运行产物 `bin` / `obj` 已忽略。

完整 API 及宿主职责见[资源管理说明](../../Documentation~/resource-management.md)。
