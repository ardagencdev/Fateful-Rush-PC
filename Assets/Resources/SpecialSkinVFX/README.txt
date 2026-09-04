PRESTIGE SKIN COIN FX / SFX

This update intentionally removes:
- Dark aura
- Golden aura
- Golden wings

Kept:
- Dash afterimage for Silver, Dark and Golden

Dark / Golden now differ through:
1) Their own sprite-based coin collection VFX
2) Their own coin collection SFX

VFX setup later:
- Dark sprite:
  Assets/Resources/SpecialSkinVFX/DarkCoinBurst.png

- Golden sprite:
  Assets/Resources/SpecialSkinVFX/GoldenCoinBurst.png

The code automatically loads these files if they exist.
You can also assign the sprites directly on SpecialSkinVisuals.

SFX setup later:
Select the object with SoundManager and assign:
- Dark Coin Sound
- Golden Coin Sound

If either custom clip is empty, that skin automatically falls back to
the normal Coin Sound.

Coin VFX animation:
- starts very small at the collected coin
- expands to slightly larger than the coin
- fades to alpha 0 while expanding
- slightly scales with coin value
- pooled to avoid repeated runtime allocations
