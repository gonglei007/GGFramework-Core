# Optional instance-owned Unity audio channels

Status: draft, unpublished. This package does not load by default from Core Optional~. It extracts actual effect voice ownership, round-robin reuse, auxiliary preview, music pause/resume and focus/application-pause gating from the current StickerGame adapter. It requires Unity audio but no third-party library.

Construct AudioChannels on the caller-owned GameObject, supply decoded AudioClips, configure effect/music enabled state, and forward focus/pause events. The game retains authored catalog paths, synthesized cue definitions, volumes, save fields, companion policy and clip destruction. Dispose releases only sources created by this instance, never the caller's clips or unrelated sources. Music volume changes remain immediate and are not converted into a new fade/duck policy.

The source preserves the game's 12 effect-voice order and lazy auxiliary voice, and does not adopt the Demo global singleton/Bundle loading. No cloud-save or full GGAudio/AudioMixer module is introduced. Validation is in progress; no device/speaker/performance acceptance is claimed.
