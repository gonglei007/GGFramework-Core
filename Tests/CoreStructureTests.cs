using System;
using NUnit.Framework;
using UnityEngine;
using GGFramework.GGTask;

/// <summary>
/// Core 规范类的结构性契约测试（纯反射，无 MonoBehaviour/线程副作用，EditMode 下稳定）。
/// 锁定各基础类的继承关系与命名空间，防止后续副本漂移再次发生。
/// </summary>
public class CoreStructureTests
{
    [Test]
    public void CoroutineUtil_DerivesFrom_SingletonOfCoroutineUtil()
    {
        Assert.That(typeof(Singleton<CoroutineUtil>).IsAssignableFrom(typeof(CoroutineUtil)), Is.True);
    }

    [Test]
    public void TaskSystem_DerivesFrom_SingletonOfTaskSystem()
    {
        Assert.That(typeof(Singleton<TaskSystem>).IsAssignableFrom(typeof(TaskSystem)), Is.True);
    }

    [Test]
    public void Singleton_IsMonoBehaviour_WithMonoBehaviourConstraint()
    {
        Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(Singleton<CoroutineUtil>)), Is.True);
    }

    [Test]
    public void TaskSystem_And_GTask_LiveIn_GGTaskNamespace()
    {
        Assert.That(typeof(TaskSystem).Namespace, Is.EqualTo("GGFramework.GGTask"));
        Assert.That(typeof(GTask).Namespace, Is.EqualTo("GGFramework.GGTask"));
    }

    [Test]
    public void Singleton_And_CoroutineUtil_AreInGlobalNamespace()
    {
        Assert.That(typeof(SingletonLifetime).Namespace, Is.Null);
        Assert.That(typeof(CoroutineUtil).Namespace, Is.Null);
    }

    [Test]
    public void CoroutineUtil_ExposesStaticDoCoroutine()
    {
        var method = typeof(CoroutineUtil).GetMethod(
            "DoCoroutine",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
    }
}
