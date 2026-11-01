using System;
using NUnit.Framework;
using GGFramework.Core;

/// <summary>
/// NetUtil 的 EditMode 单元测试（不触网，仅覆盖确定性纯函数；DNS 解析函数只移植不加用例）。
/// </summary>
public class NetUtilTests
{
    [Test]
    public void HostFromUrl_ExtractsHost()
    {
        Assert.That(NetUtil.HostFromUrl("https://a.b.com:8080/x?y=1"), Is.EqualTo("a.b.com"));
        Assert.That(NetUtil.HostFromUrl("http://example.org/path"), Is.EqualTo("example.org"));
    }

    [Test]
    public void Sign_DeterministicForSameInput()
    {
        string s1 = NetUtil.Sign("http://host/api?a=1&b=2", "secret");
        string s2 = NetUtil.Sign("http://host/api?a=1&b=2", "secret");

        Assert.That(s1, Is.EqualTo(s2));
        Assert.That(s1, Is.StringMatching("^[0-9a-f]{32}$"));
    }

    [Test]
    public void Sign_ParamOrderInsensitive()
    {
        string ascending = NetUtil.Sign("http://host/api?a=1&b=2&c=3", "secret");
        string shuffled = NetUtil.Sign("http://host/api?c=3&a=1&b=2", "secret");

        Assert.That(ascending, Is.EqualTo(shuffled));
    }

    [Test]
    public void Sign_MissingQuery_Throws()
    {
        Assert.Throws<ArgumentException>(() => NetUtil.Sign("http://host/api", "secret"));
    }
}
