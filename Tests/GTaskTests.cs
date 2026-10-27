using System.Threading;
using NUnit.Framework;
using GGFramework.GGTask;

/// <summary>
/// GTask（线程封装）的行为测试。
/// 注意：GTask.Start 先 thread.Start() 再赋值 onFinish，回调存在竞态，故此处不断言 onFinish 一定被调用，
/// 只验证 ThreadStart 会被执行以及 Dispose 的安全/幂等性。
/// </summary>
public class GTaskTests
{
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
