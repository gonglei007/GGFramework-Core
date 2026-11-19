using System.Threading;
using NUnit.Framework;
using GGFramework.GGTask;

/// <summary>
/// GTask（线程封装）的行为测试。
/// 验证执行、快速任务完成回调和释放行为。
/// </summary>
public class GTaskTests
{
    [Test]
    public void GTask_FastJob_AlwaysCallsCompletion()
    {
        for (int i = 0; i < 100; i++)
        {
            using (var finished = new ManualResetEvent(false))
            {
                var task = new GTask(() => { });
                task.Start(() => finished.Set());
                Assert.That(finished.WaitOne(3000), Is.True);
                task.Dispose();
            }
        }
    }
    [Test]
    public void GTask_Start_RunsThreadStartDelegate()
    {
        var ran = new ManualResetEvent(false);
        var task = new GTask(() => { ran.Set(); });
        task.Start(null);
        bool signalled = ran.WaitOne(3000);
        task.Dispose();
        Assert.That(signalled, Is.True, "GTask 未在超时内执行 ThreadStart 委托");
    }

    [Test]
    public void GTask_Dispose_IsIdempotentAndDoesNotThrow()
    {
        var task = new GTask(() => { });
        task.Start(null);
        Assert.DoesNotThrow(() => task.Dispose());
        // 第二次调用时内部 thread 已置 null，应为安全空操作
        Assert.DoesNotThrow(() => task.Dispose());
    }
}
