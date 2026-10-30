using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGFramework.Core
{
    /// <summary>
    /// 类型安全的全局事件总线（对标原型中大量使用的 window.dispatchEvent 解耦方式）。
    /// 以事件类型 T 作为频道键，提供 Subscribe/Unsubscribe/Publish。
    /// 采用实例化设计便于单元测试与多上下文隔离；需要全局单点时，消费方可用
    /// <see cref="Singleton{T}"/> 或静态字段自行包装（如 class GameEvent : Singleton&lt;EventBus&gt;）。
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        /// 订阅指定类型的事件。同一处理器重复订阅同一类型将被忽略（去重）。
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }
            List<Delegate> list;
            if (!_handlers.TryGetValue(typeof(T), out list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }
            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// 取消订阅指定类型的事件；未订阅时为空操作。
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }
            List<Delegate> list;
            if (_handlers.TryGetValue(typeof(T), out list))
            {
                list.Remove(handler);
            }
        }

        /// <summary>
        /// 发布指定类型的事件，依次通知所有订阅者。
        /// 派发时对订阅列表做快照，允许处理器内部安全地 Subscribe/Unsubscribe。
        /// 单个处理器抛出的异常会被捕获并记录，不影响其余处理器。
        /// </summary>
        public void Publish<T>(T evt)
        {
            List<Delegate> list;
            if (!_handlers.TryGetValue(typeof(T), out list) || list.Count == 0)
            {
                return;
            }
            Delegate[] snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    ((Action<T>)snapshot[i])(evt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// 返回指定类型当前的订阅者数量。
        /// </summary>
        public int SubscriberCount<T>()
        {
            List<Delegate> list;
            if (_handlers.TryGetValue(typeof(T), out list))
            {
                return list.Count;
            }
            return 0;
        }

        /// <summary>
        /// 清空所有类型的所有订阅。
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
