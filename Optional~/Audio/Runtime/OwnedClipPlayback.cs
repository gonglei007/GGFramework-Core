using System;
using UnityEngine;

namespace GGFramework.Core.Audio
{
    /// <summary>Plays transient clips on an existing host. Accepted clips belong to this player.</summary>
    public sealed class OwnedClipPlayback : IDisposable
    {
        readonly GameObject host;
        AudioSource source;
        bool disposed;

        public OwnedClipPlayback(GameObject host)
        {
            this.host = host ? host : throw new ArgumentNullException(nameof(host));
        }

        public AudioClip Clip { get; private set; }
        public bool IsPlaying => source && source.isPlaying;

        public void Play(AudioClip clip)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OwnedClipPlayback));
            if (!host) throw new InvalidOperationException("Playback host has been destroyed.");
            if (!clip) return;
            if (Clip != clip) Release();
            if (!source)
            {
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            Clip = clip;
            source.clip = clip;
            source.Play();
        }

        public void Stop()
        {
            if (source) source.Stop();
        }

        public void Release()
        {
            Stop();
            if (source) source.clip = null;
            if (Clip) UnityEngine.Object.Destroy(Clip);
            Clip = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Release();
            if (source) UnityEngine.Object.Destroy(source);
            source = null;
        }
    }
}
