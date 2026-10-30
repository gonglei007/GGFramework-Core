using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GGFramework.Core;

/// <summary>
/// Pool&lt;T&gt; / GameObjectPool 的 EditMode 单元测试。
/// Pool&lt;T&gt; 为纯托管对象；GameObjectPool 用例在 finally 中销毁全部实例，避免污染编辑器场景。
/// </summary>
public class ObjectPoolTests
{
    private class Box
    {
        public int Value;
    }

    // ---------- Pool<T> ----------

    [Test]
    public void Pool_Get_CreatesViaFactory_WhenEmpty()
    {
        int created = 0;
        var pool = new Pool<Box>(() => { created++; return new Box(); });

        Box b = pool.Get();

        Assert.That(b, Is.Not.Null);
        Assert.That(created, Is.EqualTo(1));
        Assert.That(pool.CountInactive, Is.EqualTo(0));
    }

    [Test]
    public void Pool_ReleaseThenGet_ReusesInstance_WithoutNewFactoryCall()
    {
        int created = 0;
        var pool = new Pool<Box>(() => { created++; return new Box(); });

        Box first = pool.Get();
        pool.Release(first);
        Assert.That(pool.CountInactive, Is.EqualTo(1));

        Box second = pool.Get();
        Assert.That(second, Is.SameAs(first));
        Assert.That(created, Is.EqualTo(1));
        Assert.That(pool.CountInactive, Is.EqualTo(0));
    }

    [Test]
    public void Pool_OnGetAndOnRelease_AreInvoked()
    {
        var log = new List<string>();
        var pool = new Pool<Box>(
            () => new Box(),
            onGet: b => log.Add("get"),
            onRelease: b => log.Add("release"));

        Box b = pool.Get();
        pool.Release(b);

        Assert.That(log, Is.EqualTo(new List<string> { "get", "release" }));
    }

    [Test]
    public void Pool_OnGet_ResetsState()
    {
        var pool = new Pool<Box>(() => new Box(), onGet: b => b.Value = 0);

        Box b = pool.Get();
        b.Value = 42;
        pool.Release(b);

        Box reused = pool.Get();
        Assert.That(reused.Value, Is.EqualTo(0));
    }

    [Test]
    public void Pool_MaxSize_DropsExcessOnRelease()
    {
        var pool = new Pool<Box>(() => new Box(), maxSize: 1);

        Box a = pool.Get();
        Box b = pool.Get();
        pool.Release(a);
        pool.Release(b);

        Assert.That(pool.CountInactive, Is.EqualTo(1));
    }

    [Test]
    public void Pool_Preload_FillsInactiveUpfront()
    {
        int created = 0;
        var pool = new Pool<Box>(() => { created++; return new Box(); }, preload: 3);

        Assert.That(pool.CountInactive, Is.EqualTo(3));
        Assert.That(created, Is.EqualTo(3));
    }

    [Test]
    public void Pool_ReleaseNull_IsIgnored()
    {
        var pool = new Pool<Box>(() => new Box());
        pool.Release(null);
        Assert.That(pool.CountInactive, Is.EqualTo(0));
    }

    [Test]
    public void Pool_Clear_EmptiesInactive()
    {
        var pool = new Pool<Box>(() => new Box(), preload: 3);
        pool.Clear();
        Assert.That(pool.CountInactive, Is.EqualTo(0));
    }

    [Test]
    public void Pool_NullFactory_Throws()
    {
        Assert.That(() => new Pool<Box>(null), Throws.ArgumentNullException);
    }

    // ---------- GameObjectPool ----------

    [Test]
    public void GameObjectPool_Get_ReturnsActiveInstance()
    {
        GameObject prefab = new GameObject("prefab");
        prefab.SetActive(false);
        var pool = new GameObjectPool(prefab);
        GameObject got = null;
        try
        {
            got = pool.Get();
            Assert.That(got, Is.Not.Null);
            Assert.That(got.activeSelf, Is.True);
        }
        finally
        {
            Cleanup(prefab, pool, got);
        }
    }

    [Test]
    public void GameObjectPool_ReleaseThenGet_ReusesInstance()
    {
        GameObject prefab = new GameObject("prefab");
        var pool = new GameObjectPool(prefab);
        GameObject first = pool.Get();
        pool.Release(first);
        Assert.That(pool.CountInactive, Is.EqualTo(1));
        Assert.That(first.activeSelf, Is.False);

        GameObject second = pool.Get();
        try
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.activeSelf, Is.True);
        }
        finally
        {
            Cleanup(prefab, pool, second);
        }
    }

    [Test]
    public void GameObjectPool_MaxSize_DropsExcessOnRelease()
    {
        GameObject prefab = new GameObject("prefab");
        var pool = new GameObjectPool(prefab, maxSize: 1);
        GameObject a = pool.Get();
        GameObject b = pool.Get();
        pool.Release(a);
        pool.Release(b);
        try
        {
            Assert.That(pool.CountInactive, Is.EqualTo(1));
        }
        finally
        {
            pool.Clear();
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void GameObjectPool_NullPrefab_Throws()
    {
        Assert.That(() => new GameObjectPool(null), Throws.ArgumentNullException);
    }

    private static void Cleanup(GameObject prefab, GameObjectPool pool, GameObject active)
    {
        if (active != null)
        {
            Object.DestroyImmediate(active);
        }
        pool.Clear();
        Object.DestroyImmediate(prefab);
    }
}
