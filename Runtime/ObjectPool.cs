using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGFramework.Core
{
    /// <summary>
    /// 泛型对象池（纯 POCO，无 UnityEngine 依赖，可在 EditMode 单元测试中安全使用）。
    /// 通过工厂委托创建实例，取出/归还时分别触发 onGet/onRelease 回调做重置。
    /// 命名放在 GGFramework.Core 下，避免与 UnityEngine.Pool.ObjectPool&lt;T&gt; 产生二义性。
    /// </summary>
    /// <typeparam name="T">被池化的类型，须为引用类型。</typeparam>
    public class Pool<T> where T : class
    {
        private readonly Stack<T> _stack = new Stack<T>();
        private readonly Func<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly int _maxSize;

        /// <summary>当前池中空闲（可复用）的实例数量。</summary>
        public int CountInactive
        {
            get { return _stack.Count; }
        }

        /// <summary>
        /// 构造对象池。
        /// </summary>
        /// <param name="factory">创建新实例的工厂委托，必填。</param>
        /// <param name="onGet">实例被取出时的重置回调，可空。</param>
        /// <param name="onRelease">实例被归还时的清理回调，可空。</param>
        /// <param name="preload">构造时预热的实例数量，默认 0。</param>
        /// <param name="maxSize">池中最多保留的空闲实例数量，超出时归还即丢弃，默认 int.MaxValue。</param>
        public Pool(Func<T> factory, Action<T> onGet = null, Action<T> onRelease = null, int preload = 0, int maxSize = int.MaxValue)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (maxSize < 0)
            {
                throw new ArgumentOutOfRangeException("maxSize");
            }
            _factory = factory;
            _onGet = onGet;
            _onRelease = onRelease;
            _maxSize = maxSize;

            int count = Mathf.Clamp(preload, 0, maxSize);
            for (int i = 0; i < count; i++)
            {
                _stack.Push(_factory());
            }
        }

        /// <summary>
        /// 从池中取出一个实例；池空时新建。取出时会触发 onGet 回调。
        /// </summary>
        public T Get()
        {
            T item = _stack.Count > 0 ? _stack.Pop() : _factory();
            if (_onGet != null)
            {
                _onGet(item);
            }
            return item;
        }

        /// <summary>
        /// 归还一个实例到池中；池已满时丢弃。归还时会触发 onRelease 回调。
        /// </summary>
        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }
            if (_onRelease != null)
            {
                _onRelease(item);
            }
            if (_stack.Count < _maxSize)
            {
                _stack.Push(item);
            }
        }

        /// <summary>
        /// 清空池中所有空闲实例（对每个空闲实例触发 onRelease 回调）。
        /// </summary>
        public void Clear()
        {
            while (_stack.Count > 0)
            {
                T item = _stack.Pop();
                if (_onRelease != null)
                {
                    _onRelease(item);
                }
            }
        }
    }

    /// <summary>
    /// GameObject 专用对象池：基于预制体克隆实例，取出时 SetActive(true)，归还时 SetActive(false) 并挂回池根节点。
    /// 在 EditMode 下销毁对象使用 DestroyImmediate，避免 Object.Destroy 在非播放态报错。
    /// </summary>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly Stack<GameObject> _stack = new Stack<GameObject>();
        private readonly int _maxSize;

        /// <summary>当前池中空闲（未激活）的实例数量。</summary>
        public int CountInactive
        {
            get { return _stack.Count; }
        }

        /// <summary>
        /// 构造 GameObject 池。
        /// </summary>
        /// <param name="prefab">用于克隆的预制体，必填。</param>
        /// <param name="root">空闲实例的挂载父节点，可空。</param>
        /// <param name="preload">构造时预热的实例数量，默认 0。</param>
        /// <param name="maxSize">池中最多保留的空闲实例数量，超出时归还即销毁，默认 int.MaxValue。</param>
        public GameObjectPool(GameObject prefab, Transform root = null, int preload = 0, int maxSize = int.MaxValue)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException("prefab");
            }
            if (maxSize < 0)
            {
                throw new ArgumentOutOfRangeException("maxSize");
            }
            _prefab = prefab;
            _root = root;
            _maxSize = maxSize;

            int count = Mathf.Clamp(preload, 0, maxSize);
            for (int i = 0; i < count; i++)
            {
                GameObject go = CreateInstance();
                go.SetActive(false);
                _stack.Push(go);
            }
        }

        private GameObject CreateInstance()
        {
            GameObject go = UnityEngine.Object.Instantiate(_prefab);
            if (_root != null)
            {
                go.transform.SetParent(_root, false);
            }
            return go;
        }

        /// <summary>
        /// 取出一个激活的实例；池空时克隆新实例。
        /// </summary>
        public GameObject Get()
        {
            GameObject go;
            if (_stack.Count > 0)
            {
                go = _stack.Pop();
                if (go == null)
                {
                    go = CreateInstance();
                }
            }
            else
            {
                go = CreateInstance();
            }
            go.SetActive(true);
            return go;
        }

        /// <summary>
        /// 归还实例：停用并挂回池根；池已满时销毁。
        /// </summary>
        public void Release(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            go.SetActive(false);
            if (_root != null)
            {
                go.transform.SetParent(_root, false);
            }
            if (_stack.Count < _maxSize)
            {
                _stack.Push(go);
            }
            else
            {
                DestroySafe(go);
            }
        }

        /// <summary>
        /// 清空池中所有空闲实例。
        /// </summary>
        public void Clear()
        {
            while (_stack.Count > 0)
            {
                GameObject go = _stack.Pop();
                if (go != null)
                {
                    DestroySafe(go);
                }
            }
        }

        private static void DestroySafe(GameObject go)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(go);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
