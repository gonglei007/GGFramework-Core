using System;
using System.Threading;
using NUnit.Framework;
using GGFramework.GGTask;
using UnityEngine;

public class TaskSystemTests
{
    [Test]
    public void TimedOutJob_DoesNotBlockFollowingJob_AndInitIsIdempotent()
    {
        int previousTimeout = TaskSystem.TaskTimeout;
        var host = new GameObject("TaskSystem timeout test");
        using (var release = new ManualResetEvent(false))
        using (var following = new ManualResetEvent(false))
        {
            try
            {
                TaskSystem.TaskTimeout = 20;
                var system = host.AddComponent<TaskSystem>();
                system.Init();
                system.Init();
                system.QueueJob(() => { release.WaitOne(3000); return null; });
                system.QueueJob(() => { following.Set(); return null; });
                Assert.That(following.WaitOne(3000), Is.True,
                    "A timed out running task must not be disposed or fault the queue pump.");
            }
            finally
            {
                release.Set();
                UnityEngine.Object.DestroyImmediate(host);
                TaskSystem.TaskTimeout = previousTimeout;
                SingletonLifetime.IsQuitting = false;
            }
        }
    }
}
