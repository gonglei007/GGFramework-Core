using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GGFramework.Core;

/// <summary>
/// EventBus 的 EditMode 单元测试（纯托管）。
/// </summary>
public class EventBusTests
{
    private struct IntEvent { public int Value; }
    private struct OtherEvent { }

    [Test]
    public void Publish_NotifiesSubscriber_WithPayload()
    {
        var bus = new EventBus();
        int received = 0;
        bus.Subscribe<IntEvent>(e => received = e.Value);

        bus.Publish(new IntEvent { Value = 42 });

        Assert.That(received, Is.EqualTo(42));
    }

    [Test]
    public void Publish_WithoutSubscriber_IsNoOp()
    {
        var bus = new EventBus();
        Assert.DoesNotThrow(() => bus.Publish(new IntEvent { Value = 1 }));
    }

    [Test]
    public void Subscribe_IsSegregatedByEventType()
    {
        var bus = new EventBus();
        int intHits = 0;
        int otherHits = 0;
        bus.Subscribe<IntEvent>(e => intHits++);
        bus.Subscribe<OtherEvent>(e => otherHits++);

        bus.Publish(new IntEvent());

        Assert.That(intHits, Is.EqualTo(1));
        Assert.That(otherHits, Is.EqualTo(0));
    }

    [Test]
    public void Unsubscribe_StopsNotifications()
    {
        var bus = new EventBus();
        int hits = 0;
        System.Action<IntEvent> handler = e => hits++;
        bus.Subscribe(handler);
        bus.Unsubscribe(handler);

        bus.Publish(new IntEvent());

        Assert.That(hits, Is.EqualTo(0));
        Assert.That(bus.SubscriberCount<IntEvent>(), Is.EqualTo(0));
    }

    [Test]
    public void Subscribe_SameHandlerTwice_IsDeduplicated()
    {
        var bus = new EventBus();
        int hits = 0;
        System.Action<IntEvent> handler = e => hits++;
        bus.Subscribe(handler);
        bus.Subscribe(handler);

        bus.Publish(new IntEvent());

        Assert.That(hits, Is.EqualTo(1));
        Assert.That(bus.SubscriberCount<IntEvent>(), Is.EqualTo(1));
    }

    [Test]
    public void Publish_NotifiesAllSubscribers_InOrder()
    {
        var bus = new EventBus();
        var log = new List<string>();
        bus.Subscribe<IntEvent>(e => log.Add("a"));
        bus.Subscribe<IntEvent>(e => log.Add("b"));

        bus.Publish(new IntEvent());

        Assert.That(log, Is.EqualTo(new List<string> { "a", "b" }));
    }

    [Test]
    public void Publish_HandlerUnsubscribingDuringDispatch_IsSafe()
    {
        var bus = new EventBus();
        var log = new List<string>();
        System.Action<IntEvent> second = e => log.Add("second");
        bus.Subscribe<IntEvent>(e =>
        {
            log.Add("first");
            bus.Unsubscribe(second);
        });
        bus.Subscribe(second);

        // 快照派发语义：本次派发不受处理器内 Unsubscribe 影响（不抛枚举修改异常），
        // 退订只对下一次派发生效。
        Assert.DoesNotThrow(() => bus.Publish(new IntEvent()));
        Assert.That(log, Is.EqualTo(new List<string> { "first", "second" }));

        bus.Publish(new IntEvent());
        Assert.That(log, Is.EqualTo(new List<string> { "first", "second", "first" }));
    }

    [Test]
    public void Publish_HandlerThrowing_DoesNotBreakOthers()
    {
        var bus = new EventBus();
        int secondHits = 0;
        bus.Subscribe<IntEvent>(e => { throw new System.Exception("boom"); });
        bus.Subscribe<IntEvent>(e => secondHits++);

        // 处理器异常被 EventBus 捕获并 Debug.LogException；测试中临时忽略失败日志断言。
        LogAssert.ignoreFailingMessages = true;
        try
        {
            bus.Publish(new IntEvent());
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }

        Assert.That(secondHits, Is.EqualTo(1));
    }

    [Test]
    public void Clear_RemovesAllSubscribers()
    {
        var bus = new EventBus();
        bus.Subscribe<IntEvent>(e => { });
        bus.Subscribe<OtherEvent>(e => { });
        bus.Clear();

        Assert.That(bus.SubscriberCount<IntEvent>(), Is.EqualTo(0));
        Assert.That(bus.SubscriberCount<OtherEvent>(), Is.EqualTo(0));
    }

    [Test]
    public void Subscribe_NullHandler_Throws()
    {
        var bus = new EventBus();
        Assert.That(() => bus.Subscribe<IntEvent>(null), Throws.ArgumentNullException);
    }
}
