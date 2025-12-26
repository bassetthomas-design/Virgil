# Virgil avatar assets

Binary PNGs for Virgil's static poses and light animations must be added manually
and are intended to be stored with Git LFS (see repository .gitattributes).

Expected structure (sizes 1024/256/64):
- `static/{size}/virgil_normal.png`
- `static/{size}/virgil_stress.png`
- `static/{size}/virgil_critical.png`
- `anim/blink/{size}/virgil_blink_01.png` ... `virgil_blink_03.png`
- `anim/glow/normal|stress|critical/{size}/virgil_glow_<state>_01.png` ... `_08.png`

Keep the filenames and folders intact; the application resolves avatars at runtime
using these paths. This repository intentionally omits the binary assets so they can
be supplied via local git push or GitHub upload while remaining tracked as LFS objects.
