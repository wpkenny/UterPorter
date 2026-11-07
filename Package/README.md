# Uter the Porter

Tired of the long walk home? Died somewhere awful and can't face the trek back to your stuff?
Call **Uter**, a grumpy Dverger porter. He appears beside you, puts you on his back, and runs you there,
grumbling (and making questionable ride-app jokes) the whole way. When the ride's over he runs off into the
distance and vanishes until you call again.

## How to use it

Type in chat:

| Say | What happens |
|---|---|
| `Uter, take me home` | He carries you home (your bed, unless you've set somewhere else). |
| `Uter, take me to my grave` | He carries you to your nearest gravestone. Also: *gravestone*, *tomb*, *body*. |
| `Uter, this is home` | Makes the spot you're standing on "home" in this world. |
| `Uter, use my bed` | Home goes back to being your bed. |

Press **Jump** at any time to climb off. He'll leave either way.

You can also set **hotkeys** for both rides in the config (unset by default).

## What he does

- **Plans the whole route before setting off**, like a sat-nav: round lakes and along coastlines,
  avoiding deep water, lava and cliffs. If something blocks the way, he plans again.
- **Sprints** (30% faster than a normal run, adjustable) and **jumps** over logs, rocks and low walls.
- **Ignores everything** on the way: monsters, loot, the lot. He's a professional.
- **Can't be hurt**, so a ride never ends with a dead porter dropping you in a swamp.
- **Grumbles.** "Is that your knee in my kidney, or have you brought a hammer?" Turn it off in the config if
  you'd rather ride in peace.
- Tells you plainly when he can't help: no bed set, no gravestone, a gravestone inside a dungeon, or no way
  over land (islands: you'll have to sail).

## Multiplayer

**Entirely client-side.** Only the player who wants rides needs the mod: not the server, not your friends.
Nobody is ever refused a server because of it. Players without the mod can't see Uter, so to them you'll
appear to be sitting in mid-air, zooming across the map. Which is, frankly, the best part.

Each player calls their own Uter, and only the player who called him can ride.

## Config

`BepInEx/config/com.wpkenny.uterporter.cfg` (or the in-game F1 menu with Configuration Manager):

| Setting | Default | What it does |
|---|---|---|
| General / Name | `Uter` | What he's called (and the name you start chat commands with). The jokes follow the name. |
| Hotkeys / TakeMeHome | none | Key to call him for a ride home. |
| Hotkeys / TakeMeToMyGrave | none | Key to call him for a ride to your gravestone. |
| Ride / SpeedBoost | `1.3` | How much faster than a normal run he goes with you aboard. |
| Ride / Grumbling | `true` | His remarks during the ride. |
| Ride / SeatHeight, SeatBehind | `0.75`, `-0.18` | Where you sit on his back. |

## Notes

- He won't go into dungeons, and won't carry you if you're already sitting on something (a chair, a cart,
  a lox).
- "Home" without a bed: sleep in a bed once, or use `Uter, this is home`.

## Requirements

BepInExPack for Valheim and Jötunn (installed automatically by mod managers).
