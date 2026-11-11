window.gameInterop = {
    _keys: {},
    _mouseDx: 0,      // accumulated mouse-look pixels since the last frame
    _fire: false,
    _weapon: 0,       // edge: 1..3 select, -1 cycle
    _reload: false,   // edge
    _pause: false,    // edge
    _mute: false,     // edge
    _canvas: null, _ctx: null, _img: null,

    startLoop(dotnetRef) {
        const g = window.gameInterop;
        // busy guard: if C# is still processing the previous tick, skip this frame instead of
        // queueing a backlog of overlapping Tick calls — dropped frames are absorbed by dt
        let busy = false;
        async function tick(timestamp) {
            window._rafId = requestAnimationFrame(tick);
            if (busy) return;
            busy = true;
            try { await dotnetRef.invokeMethodAsync('Tick', timestamp, g.readInput()); }
            finally { busy = false; }
        }
        window._rafId = requestAnimationFrame(tick);
    },

    stopLoop() {
        cancelAnimationFrame(window._rafId);
    },

    initInput() {
        const g = window.gameInterop;
        g._canvas = document.getElementById('view');
        g._ctx = g._canvas.getContext('2d');

        for (const ev of ['pointerdown', 'keydown', 'touchstart']) window.addEventListener(ev, g.unlockAudio);

        window.addEventListener('keydown', e => {
            if (e.repeat) return;
            g._keys[e.code] = true;
            if (e.code === 'Digit1') g._weapon = 1;
            if (e.code === 'Digit2') g._weapon = 2;
            if (e.code === 'Digit3') g._weapon = 3;
            if (e.code === 'KeyQ') g._weapon = -1;
            if (e.code === 'KeyR') g._reload = true;
            if (e.code === 'KeyM') g._mute = true;
            if (e.code === 'KeyP' || e.code === 'Escape') g._pause = true;
            if (e.code.startsWith('Arrow') || e.code === 'Space') e.preventDefault(); // no page scroll
        });
        window.addEventListener('keyup', e => { g._keys[e.code] = false; });

        // mouse look needs pointer lock; any element marked data-lock (canvas, Start, Resume) grabs it on click
        document.addEventListener('click', e => {
            if (e.target.closest('[data-lock]') && !g._touch && g._canvas.requestPointerLock) {
                try { const p = g._canvas.requestPointerLock(); if (p && p.catch) p.catch(() => { }); } catch (err) { }
            }
        });
        document.addEventListener('mousemove', e => {
            if (document.pointerLockElement === g._canvas) g._mouseDx += e.movementX;
        });
        g._canvas.addEventListener('mousedown', e => { if (e.button === 0) g._fire = true; });
        window.addEventListener('mouseup', e => { if (e.button === 0) g._fire = false; });
        g._canvas.addEventListener('contextmenu', e => e.preventDefault());

        // losing the mouse (Esc, alt-tab) while playing pauses the game; C# ignores the edge in menus
        document.addEventListener('pointerlockchange', () => {
            if (document.pointerLockElement !== g._canvas) { g._pause = true; g._fire = false; }
        });
    },

    // Menus need the cursor back.
    releasePointer() {
        if (document.pointerLockElement) document.exitPointerLock();
    },

    // One interop round-trip per frame: the input rides along with the Tick call.
    // [forward, strafe, keyTurn, mouseDx, fire, weaponEdge, reloadEdge, pauseEdge, muteEdge, viewW, viewH]
    readInput() {
        const g = window.gameInterop;
        const k = g._keys;
        let fwd = 0, strafe = 0, turn = 0;
        if (k['KeyW'] || k['ArrowUp']) fwd += 1;
        if (k['KeyS'] || k['ArrowDown']) fwd -= 1;
        if (k['KeyD']) strafe += 1;
        if (k['KeyA']) strafe -= 1;
        if (k['ArrowRight']) turn += 1;
        if (k['ArrowLeft']) turn -= 1;
        const fire = g._fire || k['Space'] || k['ControlLeft'];
        const out = [fwd, strafe, turn, g._mouseDx, fire ? 1 : 0, g._weapon,
            g._reload ? 1 : 0, g._pause ? 1 : 0, g._mute ? 1 : 0, window.innerWidth, window.innerHeight];
        g._mouseDx = 0;
        g._weapon = 0;
        g._reload = g._pause = g._mute = false;
        return out;
    },

    // Blit one finished frame. bytes = RGBA framebuffer from the C# renderer.
    present(bytes, w, h, sounds) {
        const g = window.gameInterop;
        if (sounds) new Set(sounds).forEach(g.sfx); // a shotgun blast can report the same sound several times
        if (!g._ctx) return;
        if (g._canvas.width !== w || g._canvas.height !== h || !g._img) {
            g._canvas.width = w;
            g._canvas.height = h;
            g._img = g._ctx.createImageData(w, h);
        }
        g._img.data.set(bytes);
        g._ctx.putImageData(g._img, 0, 0);
    },

    // ── audio: everything is synthesized with WebAudio — no sound files ──────────────────────────

    _ac: null, _master: null, _music: null, _musicTimer: null, _pendingMusic: null,
    _muted: false,

    // Browsers only allow audio after a user gesture, so the context is created on the first one.
    unlockAudio() {
        const g = window.gameInterop;
        if (g._ac) { if (g._ac.state === 'suspended' && !g._paused) g._ac.resume(); return; }
        const AC = window.AudioContext || window.webkitAudioContext;
        if (!AC) return;
        g._ac = new AC();
        g._master = g._ac.createGain();
        g._master.gain.value = g._muted ? 0 : 0.6;
        g._master.connect(g._ac.destination);
        g._music = g._ac.createGain();
        g._music.gain.value = 0.55;
        g._music.connect(g._master);
        if (g._pendingMusic !== null) g.startMusic(g._pendingMusic);
    },

    isMuted() {
        const g = window.gameInterop;
        try { g._muted = localStorage.getItem('ic_muted') === '1'; } catch (e) { }
        return g._muted;
    },

    toggleMute() {
        const g = window.gameInterop;
        g._muted = !g._muted;
        try { localStorage.setItem('ic_muted', g._muted ? '1' : '0'); } catch (e) { }
        if (g._master) g._master.gain.value = g._muted ? 0 : 0.6;
        return g._muted;
    },

    // Pausing suspends the whole context: effects stop and the music scheduler's clock freezes with it.
    setPaused(paused) {
        const g = window.gameInterop;
        g._paused = paused;
        if (!g._ac) return;
        if (paused) g._ac.suspend(); else g._ac.resume();
    },

    // One oscillator sweeping f0 → f1 with a fast decay. The building block of every tonal effect and music note.
    _tone(type, f0, f1, dur, vol, when, out, cutoff) {
        const g = window.gameInterop, ac = g._ac;
        const t = when || ac.currentTime;
        const osc = ac.createOscillator(), gain = ac.createGain();
        osc.type = type;
        osc.frequency.setValueAtTime(f0, t);
        osc.frequency.exponentialRampToValueAtTime(Math.max(1, f1), t + dur);
        gain.gain.setValueAtTime(vol, t);
        gain.gain.exponentialRampToValueAtTime(0.001, t + dur);
        let node = osc;
        if (cutoff) {
            const lp = ac.createBiquadFilter();
            lp.type = 'lowpass';
            lp.frequency.value = cutoff;
            osc.connect(lp);
            node = lp;
        }
        node.connect(gain);
        gain.connect(out || g._master);
        osc.start(t);
        osc.stop(t + dur + 0.02);
    },

    // A burst of filtered white noise: gunshots, footsteps, clicks, hi-hats.
    _noise(dur, vol, cutoff, type, when, out) {
        const g = window.gameInterop, ac = g._ac;
        const t = when || ac.currentTime;
        if (!g._noiseBuf) {
            g._noiseBuf = ac.createBuffer(1, ac.sampleRate, ac.sampleRate);
            const d = g._noiseBuf.getChannelData(0);
            for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
        }
        const src = ac.createBufferSource(), filter = ac.createBiquadFilter(), gain = ac.createGain();
        src.buffer = g._noiseBuf;
        filter.type = type || 'lowpass';
        filter.frequency.value = cutoff;
        gain.gain.setValueAtTime(vol, t);
        gain.gain.exponentialRampToValueAtTime(0.001, t + dur);
        src.connect(filter);
        filter.connect(gain);
        gain.connect(out || g._master);
        src.start(t, Math.random() * 0.5, dur + 0.02);
    },

    // ids match the C# Sfx enum order
    sfx(id) {
        const g = window.gameInterop;
        if (!g._ac || g._muted || g._ac.state !== 'running') return;
        const now = g._ac.currentTime;
        const arp = (freqs, gap, vol) => freqs.forEach((f, i) => g._tone('square', f, f, gap * 1.6, vol, now + i * gap, null, 3000));
        switch (id) {
            case 0: g._noise(0.05, 0.14, 320); break;                                                    // Step
            case 1: g._tone('sawtooth', 60, 120, 0.5, 0.14, 0, null, 700); g._noise(0.4, 0.08, 500); break; // Door
            case 2: g._tone('square', 150, 110, 0.1, 0.12); g._tone('square', 130, 90, 0.12, 0.12, now + 0.13); break; // DoorLocked
            case 3: g._noise(0.13, 0.5, 2600); g._tone('square', 320, 70, 0.09, 0.25); break;             // Pistol
            case 4: g._noise(0.38, 0.8, 1500); g._tone('sawtooth', 170, 35, 0.22, 0.4); break;            // Shotgun
            case 5: g._noise(0.07, 0.38, 3200); g._tone('square', 250, 90, 0.05, 0.2); break;             // Chaingun
            case 6: g._tone('square', 900, 700, 0.03, 0.12); break;                                       // Empty
            case 7: g._noise(0.03, 0.25, 1800, 'highpass'); g._noise(0.04, 0.3, 1200, 'highpass', now + 0.2); break; // Reload
            case 8: g._tone('sine', 660, 990, 0.1, 0.25); break;                                          // Pickup
            case 9: arp([880, 1320], 0.08, 0.14); break;                                                  // Key
            case 10: g._tone('sawtooth', 320, 150, 0.12, 0.2, 0, null, 1500); break;                      // EnemyHurt
            case 11: g._tone('sawtooth', 230, 45, 0.45, 0.28, 0, null, 1200); g._noise(0.3, 0.15, 700); break; // EnemyDie
            case 12: g._tone('sawtooth', 110, 210, 0.28, 0.22, 0, null, 900); break;                      // EnemyAlert
            case 13: g._noise(0.1, 0.3, 1800); g._tone('square', 520, 190, 0.08, 0.14); break;            // EnemyShot
            case 14: g._tone('square', 190, 85, 0.2, 0.3, 0, null, 1000); g._noise(0.1, 0.2, 900); break; // PlayerHurt
            case 15: g._tone('sawtooth', 210, 28, 1.1, 0.35, 0, null, 900); break;                        // PlayerDie
            case 16: arp([523, 659, 784, 1047], 0.11, 0.14); break;                                       // LevelDone
            case 17: arp([392, 523, 784], 0.09, 0.16); break;                                             // WeaponGet
        }
    },

    // Generated background music: a 16-step bass line, sparse lead and drums, all derived from the level seed —
    // every level has its own track and the same seed always plays the same one.
    startMusic(seed) {
        const g = window.gameInterop;
        g.stopMusic();
        if (!g._ac) { g._pendingMusic = seed; return; }
        g._pendingMusic = null;
        let s = (seed >>> 0) || 1;
        const rnd = () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296;
        const scale = [0, 3, 5, 7, 10];                           // minor pentatonic
        const root = 55 * Math.pow(2, Math.floor(rnd() * 6) / 12); // somewhere from A1 up
        const pick = () => scale[Math.floor(rnd() * scale.length)];
        const bass = Array.from({ length: 16 }, (_, i) => i % 4 === 0 ? 0 : (rnd() < 0.5 ? pick() : -1));
        const lead = Array.from({ length: 16 }, () => rnd() < 0.3 ? pick() + 24 : -1);
        const stepDur = 60 / (96 + Math.floor(rnd() * 24)) / 2;   // eighth notes at 96–119 bpm
        let step = 0, next = g._ac.currentTime + 0.1;
        g._musicTimer = setInterval(() => {
            const ac = g._ac;
            while (next < ac.currentTime + 0.3) {
                const i = step % 16, bar = step >> 4;
                if (bass[i] >= 0) g._tone('sawtooth', root * Math.pow(2, bass[i] / 12), root * Math.pow(2, bass[i] / 12), stepDur * 0.95, 0.16, next, g._music, 420);
                if (lead[i] >= 0 && bar % 4 >= 2) g._tone('square', root * Math.pow(2, lead[i] / 12), root * Math.pow(2, lead[i] / 12), stepDur * 1.8, 0.04, next, g._music, 1600);
                if (i % 8 === 0) g._tone('sine', 110, 38, 0.16, 0.3, next, g._music);        // kick
                if (i % 4 === 2) g._noise(0.04, 0.05, 6000, 'highpass', next, g._music);     // hat
                step++;
                next += stepDur;
            }
        }, 80);
    },

    stopMusic() {
        const g = window.gameInterop;
        if (g._musicTimer) clearInterval(g._musicTimer);
        g._musicTimer = null;
    }
};
