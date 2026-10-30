using System.Collections.Generic;
using NUnit.Framework;
using GGFramework.Core;

/// <summary>
/// StateMachine&lt;TState&gt; 的 EditMode 单元测试（纯托管，无 MonoBehaviour/线程副作用）。
/// </summary>
public class StateMachineTests
{
    private enum State
    {
        None,
        Idle,
        Run,
        Jump
    }

    [Test]
    public void InitialState_HasNoState()
    {
        var fsm = new StateMachine<State>();
        Assert.That(fsm.HasState, Is.False);
    }

    [Test]
    public void ChangeState_SetsCurrent_AndReturnsTrue()
    {
        var fsm = new StateMachine<State>();
        bool ok = fsm.ChangeState(State.Idle);
        Assert.That(ok, Is.True);
        Assert.That(fsm.Current, Is.EqualTo(State.Idle));
        Assert.That(fsm.HasState, Is.True);
    }

    [Test]
    public void ChangeState_ToSameState_ReturnsFalse_AndNoCallback()
    {
        var fsm = new StateMachine<State>();
        fsm.ChangeState(State.Idle);

        int enterCount = 0;
        fsm.Configure(State.Idle, onEnter: () => enterCount++);

        bool ok = fsm.ChangeState(State.Idle);
        Assert.That(ok, Is.False);
        Assert.That(enterCount, Is.EqualTo(0));
    }

    [Test]
    public void ChangeState_InvokesExitThenEnter()
    {
        var log = new List<string>();
        var fsm = new StateMachine<State>();
        fsm.Configure(State.Idle, onEnter: () => log.Add("enter-idle"), onExit: () => log.Add("exit-idle"));
        fsm.Configure(State.Run, onEnter: () => log.Add("enter-run"));

        fsm.ChangeState(State.Idle);
        fsm.ChangeState(State.Run);

        Assert.That(log, Is.EqualTo(new List<string> { "enter-idle", "exit-idle", "enter-run" }));
    }

    [Test]
    public void StateChanged_FiresWithPreviousAndNext()
    {
        var fsm = new StateMachine<State>();
        var pairs = new List<string>();
        fsm.StateChanged += (prev, next) => pairs.Add(prev + "->" + next);

        fsm.ChangeState(State.Idle);
        fsm.ChangeState(State.Run);

        Assert.That(pairs, Is.EqualTo(new List<string> { "None->Idle", "Idle->Run" }));
    }

    [Test]
    public void Update_ForwardsDeltaTime_ToCurrentStateOnly()
    {
        float idleTime = 0f;
        float runTime = 0f;
        var fsm = new StateMachine<State>();
        fsm.Configure(State.Idle, onUpdate: dt => idleTime += dt);
        fsm.Configure(State.Run, onUpdate: dt => runTime += dt);

        fsm.ChangeState(State.Idle);
        fsm.Update(0.5f);
        fsm.ChangeState(State.Run);
        fsm.Update(0.25f);

        Assert.That(idleTime, Is.EqualTo(0.5f));
        Assert.That(runTime, Is.EqualTo(0.25f));
    }

    [Test]
    public void Update_BeforeAnyState_IsNoOp()
    {
        var fsm = new StateMachine<State>();
        Assert.DoesNotThrow(() => fsm.Update(1f));
    }

    [Test]
    public void TransitionGuard_BlocksWhenFalse_AllowsWhenTrue()
    {
        bool allow = false;
        var fsm = new StateMachine<State>();
        fsm.ChangeState(State.Idle);
        fsm.AddTransitionGuard(State.Idle, State.Jump, () => allow);

        Assert.That(fsm.ChangeState(State.Jump), Is.False);
        Assert.That(fsm.Current, Is.EqualTo(State.Idle));

        allow = true;
        Assert.That(fsm.ChangeState(State.Jump), Is.True);
        Assert.That(fsm.Current, Is.EqualTo(State.Jump));
    }

    [Test]
    public void ChangeState_ToUnconfiguredState_StillWorks()
    {
        var fsm = new StateMachine<State>();
        Assert.That(fsm.ChangeState(State.Jump), Is.True);
        Assert.That(fsm.Current, Is.EqualTo(State.Jump));
    }

    [Test]
    public void Clear_ResetsState()
    {
        var fsm = new StateMachine<State>();
        fsm.ChangeState(State.Run);
        fsm.Clear();
        Assert.That(fsm.HasState, Is.False);
    }

    [Test]
    public void AddTransitionGuard_NullGuard_Throws()
    {
        var fsm = new StateMachine<State>();
        Assert.That(() => fsm.AddTransitionGuard(State.Idle, State.Run, null), Throws.ArgumentNullException);
    }
}
