# 0.4.0-preview.1 验证记录

验证日期：2026-11-20（Asia/Shanghai）。平台：Windows x64。源代码基线为 `6418f0ef6da2e3dd29f49280f4ae7b1220d9e270`，在其上检查并完善资源缓存 / 请求队列的预览修改。待测文件的 LF 规范化 SHA-256 见[机器可读记录](preview-0.4.0-validation.json)。

| 验证 | 环境 | 结果 |
| --- | --- | --- |
| SDK 资源契约 | .NET SDK 10.0.401，C# 9 | 38 通过，0 失败；严格构建 0 警告 / 0 错误 |
| 全量 Core EditMode | Unity 6000.3.13f1；Test Framework 1.6.0 | 104 通过，0 失败，0 跳过 |
| 最低声明版本回归 | Unity 2021.3.45f1；Test Framework 1.1.33 | 104 通过，0 失败，0 跳过 |
| 接入文档代码 | 从 Markdown 提取两个完整示例，.NET 10 / C# 9 | 编译、运行均通过 |

新增回归中，请求工厂同键递归、工厂取消 / 调度 / 修改队列两项在修复前失败；加入重入保护后通过。其余案例覆盖 LRU、保护预算、重复键、溢出、释放失败与重入、FIFO、去重、动态并发、同步完成、取消替换及陈旧 / 跨队列租约。

## 复现方式

SDK 契约在仓库根执行：

```powershell
dotnet run --project "Tests~/ResourceContracts/ResourceContracts.csproj" --configuration Release
dotnet build "Tests~/ResourceContracts/ResourceContracts.csproj" --configuration Release --warnaserror
```

Unity 使用两个独立空宿主工程，均通过本地 UPM 路径引用本次 Core 源码，并在 `testables` 中加入 `com.gonglei.ggframework.core`；分别配置上述版本的 Test Framework 及其 `imgui` / `jsonserialize` 模块依赖。没有修改 Demo 的稳定 Git 依赖。用对应编辑器运行：

```text
Unity.exe -batchmode -nographics -projectPath <测试宿主目录> -runTests -testPlatform EditMode -assemblyNames GGFramework.Core.Tests -testResults <XML输出路径> -logFile <日志输出路径>
```

通过 XML 的总数、失败与跳过字段核对结果，不能仅凭编辑器启动成功或宿主脚本退出码判断。测试宿主、原始 XML 和日志留在本机忽略的产物目录，不进入 UPM 包提交；导入器补写的旧脚本元数据已恢复。

这些结果验证纯托管契约、现有 Core 回归和两个具体编辑器版本；不代表真实 AssetBundle、网络取消、纹理释放或业务场景已验收。真实集成职责见[资源管理说明](resource-management.md)。本次发布到 `main` 的仍是预览包，不创建新版本标签，也不变更其他仓库的消费依赖。
