using NUnit.Framework;
using UnityEngine;

/// <summary>
/// SingletonLifetime / Singleton&lt;T&gt; 退出守卫测试。
/// 所有用例都保存并在 finally 中恢复 SingletonLifetime.IsQuitting，避免静态状态跨用例污染；
/// 且不触发 Singleton 实例创建（不产生遗留 GameObject）。
/// </summary>
public class SingletonLifetimeTests
{
    private class ProbeBehaviour : MonoBehaviour { }

    [Test]
    public void IsQuitting_IsSettableStaticBool()
    {
        bool original = SingletonLifetime.IsQuitting;
        try
        {
            SingletonLifetime.IsQuitting = false;
            Assert.That(SingletonLifetime.IsQuitting, Is.False);

            SingletonLifetime.IsQuitting = true;
            Assert.That(SingletonLifetime.IsQuitting, Is.True);
        }
        finally
        {
            SingletonLifetime.IsQuitting = original;
        }
    }

    [Test]
    public void Instance_WhenQuitting_ReturnsNull_WithoutCreatingObject()
    {
        bool original = SingletonLifetime.IsQuitting;
        try
        {
            SingletonLifetime.IsQuitting = true;
            Assert.That(Singleton<ProbeBehaviour>.Instance, Is.Null);
        }
        finally
        {
            SingletonLifetime.IsQuitting = original;
        }
    }
}
