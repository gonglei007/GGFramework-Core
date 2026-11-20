using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GGFramework.Core.Audio;

public sealed class AudioChannelsTests
{
    GameObject host, listener;
    AudioClip a,b;
    AudioChannels channels;
    bool previousPause;
    [SetUp] public void Setup()
    {
        previousPause=AudioListener.pause;AudioListener.pause=false;
        if(!UnityEngine.Object.FindFirstObjectByType<AudioListener>())listener=new GameObject("Audio contracts listener",typeof(AudioListener));
        host=new GameObject("Audio contracts owner");a=Clip("a",220);b=Clip("b",440);
        channels=new AudioChannels(host);
    }
    static AudioClip Clip(string name,float frequency)
    {
        int rate=22050;var samples=new float[rate*4];for(int i=0;i<samples.Length;i++)samples[i]=.1f*Mathf.Sin(2*Mathf.PI*frequency*i/rate);
        var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
    }
    [TearDown] public void Cleanup()
    {
        channels?.Dispose();if(host)UnityEngine.Object.DestroyImmediate(host);if(listener)UnityEngine.Object.DestroyImmediate(listener);
        if(a)UnityEngine.Object.DestroyImmediate(a);if(b)UnityEngine.Object.DestroyImmediate(b);AudioListener.pause=previousPause;
    }
    [UnityTest] public IEnumerator StartsWithAuthoredMusicUnassignedAndBoundedSources()
    {
        Assert.AreEqual(13,host.GetComponents<AudioSource>().Length);Assert.AreEqual(12,channels.EffectVoiceCount);
        Assert.IsNull(channels.MusicSource.clip);Assert.IsFalse(channels.MusicPlaying);Assert.IsTrue(channels.MusicSource.loop);
        Assert.IsTrue(host.GetComponents<AudioSource>().All(s=>!s.playOnAwake));yield return null;
    }
    [UnityTest] public IEnumerator EffectVoicesActuallyPlayAndReuseRoundRobin()
    {
        var first=host.GetComponents<AudioSource>()[0];for(int i=0;i<12;i++)Assert.IsTrue(channels.PlayEffect(a,.75f));
        Assert.IsTrue(channels.PlayEffect(b,.25f));yield return null;
        Assert.AreSame(b,first.clip);Assert.AreEqual(.25f,first.volume);Assert.IsTrue(first.isPlaying);
        Assert.AreEqual(12,channels.ActiveEffectVoices);Assert.AreEqual(13,host.GetComponents<AudioSource>().Length);
        for(int i=0;i<128;i++)channels.PlayEffect(a,.75f);Assert.AreEqual(13,host.GetComponents<AudioSource>().Length);
    }
    [UnityTest] public IEnumerator AuxiliaryIsLazyReusedAndNotPartOfEffectCap()
    {
        Assert.IsNull(channels.AuxiliarySource);Assert.IsTrue(channels.PlayAuxiliary(a,.4f));var auxiliary=channels.AuxiliarySource;yield return null;
        Assert.IsTrue(auxiliary.isPlaying);Assert.AreEqual(14,host.GetComponents<AudioSource>().Length);Assert.AreEqual(0,channels.ActiveEffectVoices);
        channels.PlayAuxiliary(b,.6f);Assert.AreSame(auxiliary,channels.AuxiliarySource);Assert.AreSame(b,auxiliary.clip);
        channels.StopAuxiliary();Assert.IsFalse(auxiliary.isPlaying);Assert.AreEqual(14,host.GetComponents<AudioSource>().Length);
    }
    [UnityTest] public IEnumerator DisablingEffectsStopsBothEffectKindsAndPreservesMusic()
    {
        channels.ReplaceMusicClip(a);channels.SetEnabled(true,true);channels.PlayEffect(b,.75f);channels.PlayAuxiliary(b,.75f);yield return new WaitForSecondsRealtime(.1f);
        Assert.IsTrue(channels.MusicPlaying);channels.SetEnabled(false,true);yield return null;
        Assert.AreEqual(0,channels.ActiveEffectVoices);Assert.IsFalse(channels.AuxiliarySource.isPlaying);Assert.IsTrue(channels.MusicPlaying);
        Assert.IsFalse(channels.PlayEffect(b,.75f));Assert.IsFalse(channels.PlayAuxiliary(b,.75f));
    }
    [UnityTest] public IEnumerator MusicPauseAndResumePreserveItsPlaybackPosition()
    {
        channels.ReplaceMusicClip(a);channels.SetEnabled(true,true);yield return new WaitForSecondsRealtime(.2f);
        Assert.IsTrue(channels.MusicPlaying);float position=channels.MusicPosition;Assert.Greater(position,.02f);
        channels.SetEnabled(true,false);yield return new WaitForSecondsRealtime(.1f);Assert.IsFalse(channels.MusicPlaying);
        Assert.AreEqual(position,channels.MusicPosition,.06f);channels.SetEnabled(true,true);yield return new WaitForSecondsRealtime(.1f);
        Assert.IsTrue(channels.MusicPlaying);Assert.Greater(channels.MusicPosition,position+.02f);
    }
    [UnityTest] public IEnumerator FocusAndApplicationPauseHaveIndependentGates()
    {
        channels.ReplaceMusicClip(a);channels.SetEnabled(true,true);channels.PlayEffect(b,.75f);channels.PlayAuxiliary(b,.75f);yield return new WaitForSecondsRealtime(.1f);
        channels.SetFocused(false);Assert.AreEqual(0,channels.ActiveEffectVoices);Assert.IsFalse(channels.MusicPlaying);Assert.IsFalse(channels.CanPlayEffects);
        channels.SetApplicationPaused(true);channels.SetFocused(true);Assert.IsFalse(channels.CanPlayEffects);Assert.IsFalse(channels.MusicPlaying);
        channels.SetApplicationPaused(false);yield return null;Assert.IsTrue(channels.CanPlayEffects);Assert.IsTrue(channels.MusicPlaying);
    }
    [UnityTest] public IEnumerator MusicReplacementReturnsCallerOwnedPreviousClip()
    {
        Assert.IsNull(channels.ReplaceMusicClip(a));channels.SetEnabled(true,true);yield return null;
        Assert.AreSame(a,channels.ReplaceMusicClip(b));Assert.IsFalse(channels.MusicPlaying);Assert.IsTrue(a);Assert.AreSame(b,channels.MusicSource.clip);
        channels.SyncMusic();yield return null;Assert.IsTrue(channels.MusicPlaying);
    }
    [UnityTest] public IEnumerator OutputVolumeIsImmediateAndSettingsDoNotInventANewDuckPolicy()
    {
        channels.ReplaceMusicClip(a);channels.SetEnabled(true,true);channels.SetMusicOutputVolume(0);yield return null;
        Assert.AreEqual(0,channels.MusicSource.volume);Assert.IsTrue(channels.MusicPlaying);
        channels.SetEnabled(false,true);Assert.AreEqual(0,channels.MusicSource.volume);
        channels.SetMusicOutputVolume(.35f);Assert.AreEqual(.35f,channels.MusicSource.volume);
    }
    [UnityTest] public IEnumerator DisposalReleasesOnlyOwnedSourcesAndKeepsCallerClips()
    {
        var unrelated=host.AddComponent<AudioSource>();unrelated.clip=b;unrelated.Play();channels.PlayEffect(a,.75f);channels.PlayAuxiliary(a,.75f);
        channels.Dispose();channels.Dispose();yield return null;
        Assert.IsTrue(channels.IsDisposed);Assert.AreEqual(1,host.GetComponents<AudioSource>().Length);Assert.IsTrue(unrelated);Assert.IsTrue(unrelated.isPlaying);
        Assert.IsTrue(a);Assert.IsTrue(b);Assert.Throws<ObjectDisposedException>(()=>channels.PlayEffect(a,.75f));
    }
    [UnityTest] public IEnumerator TwoOwnersOnOneObjectCannotDisposeEachOthersSources()
    {
        using(var other=new AudioChannels(host,2,.2f))
        {
            other.ReplaceMusicClip(b);other.SetEnabled(true,true);other.PlayEffect(b,.4f);channels.PlayEffect(a,.75f);
            channels.Dispose();yield return null;Assert.IsTrue(other.MusicPlaying);Assert.AreEqual(1,other.ActiveEffectVoices);Assert.AreEqual(3,host.GetComponents<AudioSource>().Length);
        }
    }
}
