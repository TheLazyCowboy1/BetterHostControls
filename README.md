# Better Host Controls

A Rain World mod for Rain Meadow (online multiplayer mod).

This mod is designed to function as a client-side mod for hosts,
that is, only hosts can use it, but clients don't have to have the mod enabled in order for it to work (client-side).

### Allows hosts to kill/explode players from the spectator menu

Temporarily replaces the kick/ban button with a kill/explode button.
The client will be told to explode, invoking Artificer's PyroDeath() upon themselves.

Intended Use: Prevents hosts from having to wait around for players to get into shelters or gates.
It's a nicer alternative to banning slowpokes.


### Patches gates to work when dead players are still spectating

Sends all dead players to the sleep/win screen whenever all the alive players attempt to use a gate.
This isn't a very elegant patch, but it was the easiest and least glitchy solution that I could find.