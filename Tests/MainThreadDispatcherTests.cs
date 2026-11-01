using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using GGFramework.Core;

/// <summary>
/// MainThreadDispatcher 的 EditMode 单元测试。
/// 派发 API 全为静态、不依赖 Update tick，故无需创建 GameObject 即可驱动；
/// 每例 SetUp 先 Initialize() 记录当前（测试）线程为主线程，TearDown 排空队列避免串扰。
/// </summary>
public class MainThreadDispatcherTests
{
    [SetUp]
    public void SetUp()
    {
        MainThreadDispatcher.Initialize();
        MainThreadDispatcher.ProcessQueue();
    }

    [TearDown]
    public void TearDown()
    {
        MainThreadDispatcher.ProcessQueue();
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void IsMainThread_TrueOnTestThread()
    {
        Assert.That(MainThreadDispatcher.IsMainThread, Is.True);
    }

    [Test]
    public void Post_OnMainThread_ExecutesImmediately()
    {
        bool executed = false;

        MainThreadDispatcher.Post(() => { executed = true; });

        Assert.That(executed, Is.True);
        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public void Post_NullAction_NoThrow()
    {
        Assert.DoesNotThrow(() => MainThreadDispatcher.Post(null));
        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public void Enqueue_DefersUntilProcessQueue()
    {
        bool executed = false;

        MainThreadDispatcher.Enqueue(() => { executed = true; });

        Assert.That(executed, Is.False);
        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(1));

        MainThreadDispatcher.ProcessQueue();

        Assert.That(executed, Is.True);
        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public void ProcessQueue_DrainsInFifoOrder()
    {
        List<string> order = new List<string>();

        MainThreadDispatcher.Enqueue(() => order.Add("a"));
        MainThreadDispatcher.Enqueue(() => order.Add("b"));
        MainThreadDispatcher.Enqueue(() => order.Add("c"));

        MainThreadDispatcher.ProcessQueue();

        Assert.That(order, Is.EqualTo(new List<string> { "a", "b", "c" }));
    }

    [Test]
    public void ProcessQueue_IsolatesException_ContinuesRemaining()
    {
        // ProcessQueue 内部对抛异常的回调调用 Debug.LogException，Unity Test Framework 默认会因错误日志判失败，
        // 这里显式忽略失败日志，仅验证"异常被隔离、后续回调仍执行"的行为。
        LogAssert.ignoreFailingMessages = true;

        bool afterExecuted = false;

        MainThreadDispatcher.Enqueue(() => { throw new InvalidOperationException("boom"); });
        MainThreadDispatcher.Enqueue(() => { afterExecuted = true; });

        Assert.DoesNotThrow(() => MainThreadDispatcher.ProcessQueue());
        Assert.That(afterExecuted, Is.True);
        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(0));
    }

    [Test]
    public void PendingCount_ReflectsQueue()
    {
        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(0));

        MainThreadDispatcher.Enqueue(() => { });
        MainThreadDispatcher.Enqueue(() => { });

        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(2));

        MainThreadDispatcher.ProcessQueue();

        Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(0));
    }
}
