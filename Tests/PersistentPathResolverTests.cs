using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using GGFramework.Core;

/// <summary>
/// PersistentPathResolver 的 EditMode 单元测试。
/// 可写探测用例在系统临时目录下建独立子目录，finally 中删除，避免污染机器与编辑器场景；
/// 只读校验 Candidates/Best 是否包含 Application.persistentDataPath（编辑器/非 Android 分支）。
/// </summary>
public class PersistentPathResolverTests
{
    private static string MakeTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ggcore_ppr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public void IsDirectoryWritable_TempDir_True()
    {
        string dir = MakeTempDir();
        try
        {
            Assert.That(PersistentPathResolver.IsDirectoryWritable(dir), Is.True);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public void IsDirectoryWritable_NonExistent_False()
    {
        string missing = Path.Combine(Path.GetTempPath(), "ggcore_missing_" + Guid.NewGuid().ToString("N"));

        Assert.That(Directory.Exists(missing), Is.False);
        Assert.That(PersistentPathResolver.IsDirectoryWritable(missing), Is.False);
    }

    [Test]
    public void IsDirectoryWritable_ProbeFileCleanedUp()
    {
        string dir = MakeTempDir();
        try
        {
            Assert.That(PersistentPathResolver.IsDirectoryWritable(dir), Is.True);
            Assert.That(Directory.GetFiles(dir), Is.Empty, "探针文件应被删除，目录应无残留");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public void FileExists_TrueForRealFile_FalseForMissing()
    {
        string dir = MakeTempDir();
        try
        {
            string relative = "probe.txt";
            File.WriteAllText(Path.Combine(dir, relative), "x");

            Assert.That(PersistentPathResolver.FileExists(dir, relative), Is.True);
            Assert.That(PersistentPathResolver.FileExists(dir, "not_exist.txt"), Is.False);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public void Candidates_IncludesApplicationPersistentDataPath_AndBestNotNull()
    {
        string unityPath = Application.persistentDataPath;

        string[] candidates = PersistentPathResolver.Candidates;

        Assert.That(candidates, Does.Contain(unityPath));
        Assert.That(PersistentPathResolver.Best, Is.Not.Null.And.Not.Empty);
    }
}
