# Audio

All sound is synthesized with WebAudio in `gameInterop.js`: no audio files, no libraries. Players never wait on a download and the repo stays code-only.

## Context lifecycle

Browsers block audio until a user gesture, so `unlockAudio()` is bound to the first `pointerdown`, `keydown` or `touchstart` and creates the `AudioContext` then. Music requested earlier waits in `_pendingMusic`. Pausing calls `suspend()` (the music scheduler's clock freezes with it); resuming calls `resume()`; hiding the tab suspends immediately.

Mute is a master gain of 0, persisted in `localStorage["ic_muted"]`, toggled with `M` or the speaker icon.

## Effects

C# raises `Sfx` events during `Tick`; `Game.razor` collects the ids into one array per frame and passes it to `present`, which plays each distinct id once (a 7-pellet shotgun blast reports one sound, not seven).

Ids follow the enum order in `Engine.cs`:

| id | Sfx | id | Sfx |
|---|---|---|---|
| 0 | Step | 9 | Key |
| 1 | Door | 10 | EnemyHurt |
| 2 | DoorLocked | 11 | EnemyDie |
| 3 | Pistol | 12 | EnemyAlert |
| 4 | Shotgun | 13 | EnemyShot |
| 5 | Chaingun | 14 | PlayerHurt |
| 6 | Empty | 15 | PlayerDie |
| 7 | Reload | 16 | LevelDone |
| 8 | Pickup | 17 | WeaponGet |

Two helpers build everything: `_tone(type, f0, f1, dur, vol, when, out, cutoff)` (an oscillator sweeping f0 to f1 with a fast decay and an optional low-pass) and `_noise(dur, vol, cutoff, type, when, out)` (filtered noise from a 1-second random buffer). Gunshots are noise plus a falling square tone; the door is a low saw plus noise; pickups and keys are short rising sines/arpeggios.

**When adding a sound:** add the enum value at the end of `Sfx`, add the matching `case` in `sfx()`, and raise it with `Sounds.Add(...)`.

## Music

`startMusic(seed)` generates a track from a seeded linear congruential generator, so **each level has its own tune and the same level always plays the same one**:
- root note somewhere from A1 upward, minor pentatonic scale,
- a 16-step bass line (the beat always plays root on every 4th step),
- a sparse lead that only enters in the 3rd and 4th bar of every 4,
- kick every 8 steps and a hi-hat on the off-beats,
- tempo 96-119 BPM.

A 80 ms timer schedules notes 0.3 s ahead on the audio clock, the standard way to keep WebAudio rhythm steady despite JS timer jitter. Music has its own gain (0.55) under the master.

## Verification status

Automated checks confirmed all 18 effects run without errors, the music scheduler starts and stops, mute drives the gain to 0 and persists, and pause suspends the context. **Whether it sounds good has not been judged by a person** (GitHub issue open).
