using System;
using UnityEngine;

namespace GGFramework.Core.Audio
{
    /// <summary>Instance-owned Unity sources. The caller owns clip loading, clip lifetime and product cues.</summary>
    public sealed class AudioChannels : IDisposable
    {
        readonly GameObject owner;
        readonly AudioSource[] effects;
        readonly AudioSource music;
        AudioSource auxiliary;
        int nextVoice;
        bool effectsEnabled = true, musicEnabled, focused = true, paused, disposed;
        public AudioSource MusicSource => music;
        public AudioSource AuxiliarySource => auxiliary;
        public int EffectVoiceCount => effects.Length;
        public int ActiveEffectVoices
        {
            get { int count=0; foreach(var source in effects) if(source && source.isPlaying) count++; return count; }
        }
        public bool CanPlayEffects => !disposed && effectsEnabled && focused && !paused;
        public bool MusicPlaying => music && music.isPlaying;
        public float MusicPosition => music ? music.time : 0;
        public bool IsDisposed => disposed;
        public AudioChannels(GameObject owner, int effectVoiceCount = 12, float musicVolume = .35f)
        {
            if(!owner)throw new ArgumentNullException(nameof(owner));
            if(effectVoiceCount<=0)throw new ArgumentOutOfRangeException(nameof(effectVoiceCount));
            this.owner=owner;effects=new AudioSource[effectVoiceCount];
            for(int i=0;i<effects.Length;i++){effects[i]=owner.AddComponent<AudioSource>();effects[i].playOnAwake=false;}
            music=owner.AddComponent<AudioSource>();music.playOnAwake=false;music.loop=true;music.volume=musicVolume;
            // No startup replacement tune: the caller supplies its authored music.
            music.clip=null;
        }
        void RequireAlive(){if(disposed)throw new ObjectDisposedException(nameof(AudioChannels));}
        public void SetEnabled(bool effectsOn,bool musicOn)
        {
            RequireAlive();effectsEnabled=effectsOn;musicEnabled=musicOn;SyncMusic();
            if(!effectsEnabled)StopEffects();
        }
        public void SetFocused(bool focus)
        {RequireAlive();focused=focus;if(!focus)StopEffects();SyncMusic();}
        public void SetApplicationPaused(bool value)
        {RequireAlive();paused=value;if(value)StopEffects();SyncMusic();}
        public void SetMusicOutputVolume(float volume){RequireAlive();if(music)music.volume=volume;}
        public AudioClip ReplaceMusicClip(AudioClip clip)
        {RequireAlive();var previous=music.clip;music.Stop();music.clip=clip;return previous;}
        public void SyncMusic()
        {
            RequireAlive();if(!music)return;
            if(musicEnabled&&focused&&!paused&&music.clip){if(!music.isPlaying)music.UnPause();if(!music.isPlaying)music.Play();}
            else music.Pause();
        }
        public bool PlayEffect(AudioClip clip,float volume)
        {
            RequireAlive();if(!CanPlayEffects)return false;
            var voice=effects[nextVoice++%effects.Length];voice.Stop();voice.clip=clip;voice.volume=volume;voice.Play();return true;
        }
        public void StopAuxiliary(){if(auxiliary)auxiliary.Stop();}
        public bool PlayAuxiliary(AudioClip clip,float volume)
        {
            RequireAlive();StopAuxiliary();if(!CanPlayEffects)return false;
            auxiliary=auxiliary?auxiliary:owner.AddComponent<AudioSource>();auxiliary.playOnAwake=false;
            auxiliary.clip=clip;auxiliary.volume=volume;auxiliary.Play();return true;
        }
        public void StopEffects(){StopAuxiliary();foreach(var voice in effects)if(voice)voice.Stop();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;StopEffects();if(music)music.Stop();
            foreach(var voice in effects)Release(voice);Release(music);Release(auxiliary);
        }
        static void Release(AudioSource source)
        {if(source){if(Application.isPlaying)UnityEngine.Object.Destroy(source);else UnityEngine.Object.DestroyImmediate(source);}}
    }
}
