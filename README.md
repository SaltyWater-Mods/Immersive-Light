# Immersive Light

**Visibility aware block light for Vintage Story**

Immersive Light makes block light respect walls and closed barriers.

Vintage Story block light normally spreads through nearby blocks in a very gamey way. In enclosed spaces it can make light feel like it is leaking through walls instead of coming from the actual source. We cant have that.

This mod changes that. Light still uses the normal game lighting system, but each block now has to prove it can actually see the light source before it receives direct light.

## Small story

I spent a stupid amount of time wrapping my head around ideas like ray tracing, path tracing, bouncing light, custom light volumes etc
Then the simple solution slapped me in the face. Instead of rewriting light from scratch, the mod lets vanilla do most of its normal block light work, but adds one question: can this block see the light source?

If yes, light spreads there.

If no, it gets blocked, except for a tiny controlled spill around openings so doors and small gaps do not look blocky.

Turned out pretty darn great!!

## What it does not do

This is not a shader mod.

It does not replace the whole lighting engine, add path traced lighting, or simulate proper bounced light. The goal is smaller and more practical: make normal block light behave better indoors.

## Debug commands

The mod includes debug tools for checking what light is doing!

.ildebug rays on
.ildebug rays off
.ildebug clear
.ildebug clear all
.ildebug show all on
.ildebug show green off
.ildebug stats
.ildebug legend

## Author

SaltyWater
