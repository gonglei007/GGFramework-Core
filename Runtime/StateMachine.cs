using System;
using System.Collections.Generic;

namespace GGFramework.Core
{
    /// <summary>
    /// 零第三方依赖的轻量状态机（对标并替代 GGUser 借用的 MonsterLove.StateMachine）。
    /// 以枚举（或任意值类型）作为状态标识，通过委托注册各状态的进入/退出/更新回调，
    /// 支持状态转移守卫。纯托管对象，不依赖 MonoBehaviour，可在 EditMode 单元测试中安全使用。
    /// </summary>
    /// <typeparam name="TState">状态标识类型，通常为枚举，须为值类型。</typeparam>
    public class StateMachine<TState> where TState : struct
    {
        private class StateBehaviour
        {
            public Action OnEnter;
            public Action OnExit;
            public Action<float> OnUpdate;
        }

        private struct Transition : IEquatable<Transition>
        {
            public TState From;
            public TState To;

            public Transition(TState from, TState to)
            {
                From = from;
                To = to;
            }

            public bool Equals(Transition other)
            {
                return EqualityComparer<TState>.Default.Equals(From, other.From)
                    && EqualityComparer<TState>.Default.Equals(To, other.To);
            }

            public override bool Equals(object obj)
            {
                return obj is Transition && Equals((Transition)obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<TState>.Default.GetHashCode(From) * 397
                    ^ EqualityComparer<TState>.Default.GetHashCode(To);
            }
        }

        private readonly Dictionary<TState, StateBehaviour> _states = new Dictionary<TState, StateBehaviour>();
        private readonly Dictionary<Transition, Func<bool>> _guards = new Dictionary<Transition, Func<bool>>();
        private readonly EqualityComparer<TState> _comparer = EqualityComparer<TState>.Default;

        private TState _current;
        private bool _hasCurrent;

        /// <summary>状态发生转移时触发，参数为 (上一个状态, 新状态)。首次进入时上一个状态为默认值。</summary>
        public event Action<TState, TState> StateChanged;

        /// <summary>当前状态；尚未进入任何状态时为 TState 默认值。</summary>
        public TState Current
        {
            get { return _current; }
        }

        /// <summary>是否已进入某个状态。</summary>
        public bool HasState
        {
            get { return _hasCurrent; }
        }

        /// <summary>
        /// 注册（或覆盖）某状态的回调。未注册回调的状态仍可正常进入，只是不触发任何动作。
        /// </summary>
        /// <param name="state">目标状态。</param>
        /// <param name="onEnter">进入状态回调，可空。</param>
        /// <param name="onExit">退出状态回调，可空。</param>
        /// <param name="onUpdate">状态内每帧更新回调（接收 deltaTime），可空。</param>
        public void Configure(TState state, Action onEnter = null, Action onExit = null, Action<float> onUpdate = null)
        {
            StateBehaviour behaviour;
            if (!_states.TryGetValue(state, out behaviour))
            {
                behaviour = new StateBehaviour();
                _states[state] = behaviour;
            }
            behaviour.OnEnter = onEnter;
            behaviour.OnExit = onExit;
            behaviour.OnUpdate = onUpdate;
        }

        /// <summary>
        /// 为一次状态转移注册守卫：仅当 guard 返回 true 时才允许 from→to 的转移。
        /// </summary>
        public void AddTransitionGuard(TState from, TState to, Func<bool> guard)
        {
            if (guard == null)
            {
                throw new ArgumentNullException("guard");
            }
            _guards[new Transition(from, to)] = guard;
        }

        /// <summary>
        /// 切换到目标状态。
        /// 返回 false 的情况：目标与当前状态相同；或存在转移守卫且守卫返回 false。
        /// 切换时依次触发：旧状态 OnExit → 新状态 OnEnter → StateChanged 事件。
        /// </summary>
        public bool ChangeState(TState next)
        {
            if (_hasCurrent && _comparer.Equals(_current, next))
            {
                return false;
            }

            if (_hasCurrent)
            {
                Func<bool> guard;
                if (_guards.TryGetValue(new Transition(_current, next), out guard) && !guard())
                {
                    return false;
                }
            }

            TState previous = _current;

            if (_hasCurrent)
            {
                StateBehaviour exitBehaviour;
                if (_states.TryGetValue(previous, out exitBehaviour) && exitBehaviour.OnExit != null)
                {
                    exitBehaviour.OnExit();
                }
            }

            _current = next;
            _hasCurrent = true;

            StateBehaviour enterBehaviour;
            if (_states.TryGetValue(next, out enterBehaviour) && enterBehaviour.OnEnter != null)
            {
                enterBehaviour.OnEnter();
            }

            if (StateChanged != null)
            {
                StateChanged(previous, next);
            }

            return true;
        }

        /// <summary>
        /// 驱动当前状态的每帧更新；未进入状态或当前状态无 OnUpdate 时为空操作。
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_hasCurrent)
            {
                return;
            }
            StateBehaviour behaviour;
            if (_states.TryGetValue(_current, out behaviour) && behaviour.OnUpdate != null)
            {
                behaviour.OnUpdate(deltaTime);
            }
        }

        /// <summary>
        /// 清除所有状态回调与转移守卫，并复位当前状态。
        /// </summary>
        public void Clear()
        {
            _states.Clear();
            _guards.Clear();
            _current = default(TState);
            _hasCurrent = false;
        }
    }
}
