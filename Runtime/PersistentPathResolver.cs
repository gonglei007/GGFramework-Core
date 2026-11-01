using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GGFramework.Core
{
    /// <summary>
    /// 跨设备可写持久化路径解析器（重点解决 Android 分区存储下 <c>Application.persistentDataPath</c>
    /// 未必可写、以及内/外部存储路径回退的问题）。纯静态、零第三方依赖。
    ///
    /// 编辑器与非 Android 平台直接回退到 <see cref="Application.persistentDataPath"/>；
    /// Android 真机上依次探测内部/外部候选路径并做可写校验，返回 best-first 的候选列表。
    /// </summary>
    public static class PersistentPathResolver
    {
        private static string[] _candidates;

        /// <summary>
        /// 判断目录是否真实可写：目录须存在，且能在其中创建并删除一个随机探针文件。
        /// 任意异常（无权限/IO 错误）都视为不可写并返回 false。
        /// </summary>
        public static bool IsDirectoryWritable(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    return false;
                }
                string probe = Path.Combine(path, Path.GetRandomFileName());
                using (FileStream fs = File.Create(probe, 1))
                {
                }
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 内部存储持久化路径。Android 真机依次探测 emulated/data 两处；编辑器与其它平台回退
        /// <see cref="Application.persistentDataPath"/>。均不可用时返回 null。
        /// </summary>
        public static string Internal
        {
            get
            {
#if UNITY_ANDROID
                if (Application.isEditor || !Application.isPlaying)
                {
                    return Application.persistentDataPath;
                }
                string path = GetPersistentDataPath("storage", "emulated", "0", "Android", "data", Application.identifier, "files");
                if (string.IsNullOrEmpty(path))
                {
                    path = GetPersistentDataPath("data", "data", Application.identifier, "files");
                }
                return path;
#else
                return Application.persistentDataPath;
#endif
            }
        }

        /// <summary>
        /// 外部存储持久化路径。仅 Android 真机探测 sdcard0/sdcard1/mnt 三处；其它平台返回 null。
        /// </summary>
        public static string External
        {
            get
            {
#if UNITY_ANDROID
                if (Application.isEditor || !Application.isPlaying)
                {
                    return null;
                }
                string path = GetPersistentDataPath("storage", "sdcard0", "Android", "data", Application.identifier, "files");
                if (string.IsNullOrEmpty(path))
                {
                    path = GetPersistentDataPath("storage", "sdcard1", "Android", "data", Application.identifier, "files");
                }
                if (string.IsNullOrEmpty(path))
                {
                    path = GetPersistentDataPath("mnt", "sdcard", "Android", "data", Application.identifier, "files");
                }
                return path;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// best-first 的候选持久化路径列表：Internal → External → <see cref="Application.persistentDataPath"/>，
        /// 去空去重后缓存。可能为空数组（极端情况下无任何可用路径）。
        /// </summary>
        public static string[] Candidates
        {
            get
            {
                if (_candidates == null)
                {
                    List<string> paths = new List<string>();
                    string internalPath = Internal;
                    if (!string.IsNullOrEmpty(internalPath))
                    {
                        paths.Add(internalPath);
                    }
                    string externalPath = External;
                    if (!string.IsNullOrEmpty(externalPath) && !paths.Contains(externalPath))
                    {
                        paths.Add(externalPath);
                    }
                    string unityPath = Application.persistentDataPath;
                    if (!string.IsNullOrEmpty(unityPath) && !paths.Contains(unityPath))
                    {
                        paths.Add(unityPath);
                    }
                    _candidates = paths.ToArray();
                }
                return _candidates;
            }
        }

        /// <summary>最优（首个）候选持久化路径；无可用路径时返回 null。</summary>
        public static string Best
        {
            get
            {
                string[] candidates = Candidates;
                return candidates.Length > 0 ? candidates[0] : null;
            }
        }

        /// <summary>判断 <paramref name="root"/> 目录下的 <paramref name="relativePath"/> 文件是否存在。</summary>
        public static bool FileExists(string root, string relativePath)
        {
            return !string.IsNullOrEmpty(root)
                   && Directory.Exists(root)
                   && File.Exists(Path.Combine(root, relativePath));
        }

        /// <summary>
        /// 由路径分量拼出以分隔符开头的绝对路径，确保父目录存在、按需创建目标目录并做可写校验；
        /// 任意环节失败返回 null。
        /// </summary>
        private static string GetPersistentDataPath(params string[] components)
        {
            try
            {
                string path = Path.DirectorySeparatorChar + string.Join("" + Path.DirectorySeparatorChar, components);
                DirectoryInfo parent = Directory.GetParent(path);
                if (parent == null || !parent.Exists)
                {
                    return null;
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                if (!IsDirectoryWritable(path))
                {
                    return null;
                }
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }
    }
}
