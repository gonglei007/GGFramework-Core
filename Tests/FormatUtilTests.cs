using NUnit.Framework;
using GGFramework.Core;

/// <summary>
/// FormatUtil 的 EditMode 单元测试（纯函数、确定性）。
/// </summary>
public class FormatUtilTests
{
    [Test]
    public void DataSize_FormatsBytesAndScalesThroughUnits()
    {
        Assert.That(FormatUtil.DataSize(0), Is.EqualTo("0 B"));
        Assert.That(FormatUtil.DataSize(512), Is.EqualTo("512 B"));
        Assert.That(FormatUtil.DataSize(1024), Is.EqualTo("1 KB"));
        Assert.That(FormatUtil.DataSize(1536), Is.EqualTo("1.5 KB"));
        Assert.That(FormatUtil.DataSize(1024 * 1024), Is.EqualTo("1 MB"));
        Assert.That(FormatUtil.DataSize(1024d * 1024 * 1024), Is.EqualTo("1 GB"));
    }

    [Test]
    public void UnixTimestampMs_PositiveAndCloseToNow()
    {
        long before = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long ts = FormatUtil.UnixTimestampMs();
        long after = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.That(ts, Is.GreaterThan(0));
        Assert.That(ts, Is.InRange(before - 5000, after + 5000));
    }

    [Test]
    public void Md5Hex_KnownVector()
    {
        Assert.That(FormatUtil.Md5Hex("abc"), Is.EqualTo("900150983cd24fb0d6963f7d28e17f72"));
    }

    [Test]
    public void Md5Hex_IsLowercase32Hex()
    {
        string hash = FormatUtil.Md5Hex("GGFramework");

        Assert.That(hash, Has.Length.EqualTo(32));
        Assert.That(hash, Is.EqualTo(hash.ToLowerInvariant()));
        Assert.That(hash, Is.StringMatching("^[0-9a-f]{32}$"));
    }
}
