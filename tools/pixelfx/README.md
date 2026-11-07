# PixelFX trial exporter

Exports three transparent atlases for IGNIS from [patch-gg/pixelfx](https://github.com/patch-gg/pixelfx), pinned to `a84159688af3ca37a4be05a0d422b94451b2e24e`.

```powershell
git clone https://github.com/patch-gg/pixelfx.git .local-tools/pixelfx
git -C .local-tools/pixelfx checkout a84159688af3ca37a4be05a0d422b94451b2e24e
node tools/pixelfx/generate.mjs
node tools/pixelfx/generate-combat.mjs
```

Node 18+; no npm install. The exporter checks the commit and requires unchanged upstream tracked files. It imports the local renderer, writes only `unity/Assets/Resources/Art/Effects/v070`, and performs no network requests. The third-party checkout is ignored; the pinned exporter and resulting PNGs/manifest/licenses are tracked.

- `melee/sword-slash`: warm metal palette, low glow, sampled from contact onward into eight frames. Playback follows the existing hit event; no added windup.
- `holy/heal`: empty actor mask removes the stock body's recoloring; sage palette and sixteen frames. Game animation is 0.64 seconds.
- `frozen`: custom open-sided crystals using PixelFX's painter and sparkle helpers. Sixteen-frame persistent shimmer. No stock body, dense central ice block, or repeated shattering.
- Two transparent pixels surround each atlas cell; low-alpha haze below 12/255 is discarded. Source pixels remain straight-alpha RGBA. No gallery fonts are included.
- MIT license and upstream notices are included beside the runtime exports, also available to the app as Resources text assets. Sword/holy helper recipes include Hugo Duprez's Sprite Fusion attribution.

Validate/build: Unity batch `-executeMethod Expedition.EditorTools.PixelBattleEffectsValidation.ValidateAndBuild`. The device-only `pixel-effects-audit.flag` runs a temporary battle fixture at 1×, 2× and reduced flash/motion; it never saves a run or settings.

## v0.71 combat extension

`generate-combat.mjs` writes 16 eight-frame atlases to `Art/Effects/v071`, using the same pinned renderer and MIT notices. Arrow, fireball, ice lance and blunt hit adapt upstream recipes; holy/venom orbs, stone, elemental contact shards, claws, shield block and ground motes use custom painter shapes. Low saturation, limited glow and two-pixel gutters are retained.

`CombatEffectProfile` maps approved weapon art to effects. Contact effects start on `Damage`, not `BasicHit`/`SkillUsed`; periodic damage stays small. `CombatSpriteEffects` caches textures and clips projectile tails to distance travelled. No combat rules or saves change.

Validate/build: `Expedition.EditorTools.CombatSpriteEffectsValidation.ValidateAndBuild`, including the existing 120-case ally timing regression. Development-only `combat-effects-audit.flag` runs seven device fixtures across six bosses and a reduced-motion/flash repeat, without saving player progress.
