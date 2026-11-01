using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace GGFramework.Core
{
    /// <summary>
    /// 主线程派发器：把工作线程（网络/IO/Task 等）产生的回调安全地转回 Unity 主线程执行。
    ///
    /// 设计要点：
    /// - 队列为静态 <see cref="ConcurrentQueue{T}"/>，派发 API 全为静态方法，因此 EditMode 单元测试
    ///   无需创建 GameObject、也不依赖 <c>Update</c> 每帧 tick（EditMode 下 Update 不运行），
    ///   可直接调用 <see cref="ProcessQueue"/> 手动驱动。
    /// - MonoBehaviour 部分仅用于在运行时每帧泵一次队列，并由 <see cref="Instance"/> 懒创建宿主对象。
    /// - 主线程 ID 由静态字段初始化器给出安全默认值，并在 <see cref="Initialize"/> / <see cref="Instance"/>
    ///   创建时于主线程重新记录，避免"仅靠字段初始化器在任意首次触碰线程取值"的隐患。
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        // 安全默认值：类型首次被触碰时所在线程（运行时几乎总是 Unity 主线程，测试时为 NUnit 主线程）。
        private static int _mainThreadId = Thread.CurrentThread.ManagedThreadId;

        private static MainThreadDispatcher _instance;

        /// <summary>当前调用是否发生在已记录的主线程上。</summary>
        public static bool IsMainThread
        {
            get { return _mainThreadId == Thread.CurrentThread.ManagedThreadId; }
        }

        /// <summary>当前待处理（已入队但尚未排空）的回调数量。</summary>
        public static int PendingCount
        {
            get { return _queue.Count; }
        }

        /// <summary>
        /// 在主线程显式记录主线程 ID（幂等）。宿主创建时会自动调用；
        /// 若消费方在自定义启动流程中更早确定主线程，也可手动调用一次。
        /// </summary>
        public static void Initialize()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// 懒创建宿主 GameObject 与组件，用于运行时每帧泵队列。
        /// <c>DontDestroyOnLoad</c> 仅在播放模式合法，编辑器（含 EditMode 测试）中调用会抛异常，故加播放状态守卫。
        /// </summary>
        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 触发派发器时通常已在主线程，这里重新记录一次以纠正字段初始化器的默认取值。
                    Initialize();

                    GameObject host = new GameObject("(singleton) MainThreadDispatcher");
                    _instance = host.AddComponent<MainThreadDispatcher>();

                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(host);
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 派发一个回调：若当前已在主线程则立即执行，否则入队等待下一次 <see cref="ProcessQueue"/>。
        /// null 回调静默忽略。
        /// </summary>
        public static void Post(Action action)
        {
            if (action == null)
            {
                return;
            }
            if (IsMainThread)
            {
                action();
            }
            else
            {
                _queue.Enqueue(action);
            }
        }

        /// <summary>
        /// 无条件入队，等待主线程下一次 <see cref="ProcessQueue"/> 时执行（即使当前已在主线程也不立即执行）。
        /// null 回调静默忽略。
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }
            _queue.Enqueue(action);
        }

        /// <summary>
        /// 排空队列并逐个执行。单个回调抛出的异常被隔离（记录日志）而不阻断后续回调，
        /// 避免一个坏回调卡死整条主线程派发链。由 <see cref="Update"/> 每帧调用；EditMode 测试可手动调用。
        /// </summary>
        public static void ProcessQueue()
        {
            Action action;
            while (_queue.TryDequeue(out action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void Update()
        {
            ProcessQueue();
        }
    }
}
