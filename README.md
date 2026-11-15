# Rage Engine (Blade & Sorcery: Nomad)

NPC physics, behaviour and gore overhaul for B&S Nomad (Quest).

**Install (no PC needed):** open the [latest release](../../releases/latest) in the Quest browser, download the zip, and install it with Mobile VR Station (or copy the `RageEngine` folder into `Android/data/com.Warpfrog.BladeAndSorcery/files/Mods`).

- `src/RageEngine` - the mod's C# (ThunderScript)
- `src/ThunderRoadStub` - compile-time declarations of the game API (not shipped; the game supplies the real ThunderRoad.dll)
- `mod/RageEngine` - items, models, textures, sounds, maps
- GitHub Actions builds the DLL and publishes `RageEngine-<version>.zip` on every push to main.
