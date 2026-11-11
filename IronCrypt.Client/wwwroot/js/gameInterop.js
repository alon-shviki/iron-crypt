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
    present(bytes, w, h) {
        const g = window.gameInterop;
        if (!g._ctx) return;
        if (g._canvas.width !== w || g._canvas.height !== h || !g._img) {
            g._canvas.width = w;
            g._canvas.height = h;
            g._img = g._ctx.createImageData(w, h);
        }
        g._img.data.set(bytes);
        g._ctx.putImageData(g._img, 0, 0);
    }
};
