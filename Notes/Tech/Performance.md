# Performance

Target: 60 FPS on desktop and phones. Measured on the **Release docker image** served by nginx, in headless Chrome, 5 seconds of walking, turning and firing:

| Viewport | Internal size | C# time per frame (avg) | FPS |
|---|---|---|---|
| 1920 × 1080 | 386 × 200 | 3.1 ms | 60 |
| 390 × 844 (phone portrait) | 200 × 302 | 2.3 ms | 60 |
| 2560 × 1080 (ultrawide) | 480 × 200 | 3.7 ms | 60 |

(A 16.7 ms frame budget, so about 5× headroom. Worst single frames were ~35 ms, which are GC and JIT hiccups, not steady cost.)

This was a desktop browser on a PC. A phone CPU is slower: see the open issue about playtesting on a real device.

## Why it is fast enough

- **Low internal resolution:** 200 px on the short side, upscaled by the browser for free.
- **Pre-shaded textures:** 16 brightness copies per texture so the wall loop does no color math.
- **Column-major textures + fixed-point stepping:** sequential memory, no floating point per pixel.
- **One interop crossing each way per frame** ([[Tech/Input and Interop]]), the output as one `byte[]`.
- **No per-frame allocations in the hot path** for rendering; the framebuffer and tables are allocated once and on resize.
- **HUD updates are cached:** the DOM text is rebuilt only when a value changed; the FPS counter updates once a second.
- **Pathfinding amortized:** one BFS every half second for all enemies.
- **Caps:** blood droplets (120), enemies (40).

## What was deliberately not done

- **AOT compilation:** would speed up C#, but needs the `wasm-tools` workload in CI and the Dockerfile. Revisit if phones struggle.
- **Web Worker:** Bullet Heaven uses one; here the whole frame is ~3 ms so there is nothing to offload.
- **Floor casting** is not in because the flat floor is almost free; see the open issue for when it is added (watch this budget).

## If something gets slow

1. Lower the 200 px short side in `Renderer.SizeFor` (try 160).
2. Cap the long side lower.
3. Try AOT.
4. Profile `DrawSprites`: with 40 enemies near, overdraw is the likely cost.
