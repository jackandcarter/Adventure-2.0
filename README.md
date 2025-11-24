# Adventure 2.0

Adventure 2.0 is a client/server action RPG prototype built with a Unity 6.2 top-down client and an authoritative MariaDB-backed server. The project emphasizes party lobby flows, procedural dungeons, data-driven RPG systems, and an editor-first UI approach.

## Repository layout
- `Unity/` and `Assets/`: Unity client code, data, and prefabs (including the procedural `DungeonGenerationManager`).
- `Server/`: Authoritative services for sessions, parties, chat, lobby orchestration, dungeon simulation, repositories, and transport contracts.
- `Shared/`: DTOs and messaging envelope definitions shared by client and server.
- `db/`: Migration scripts defining accounts, characters, inventory, unlocks, and dungeon run logging tables.
- `Dev Guide/`: 10-part design and requirements guide outlining gameplay pillars and editor/UI expectations.

## Current status
- Core runtime services exist (session manager, lobby/dungeon orchestrators, tick-based combat and ability loop).
- Persistence interfaces and MariaDB migrations cover accounts, characters, inventory, unlocks, dungeon runs, and run events.
- Networking envelopes and DTOs are defined end-to-end, with client-side queuing/heartbeat/reconnect plumbing ready for a real transport.

## Roadmap and next steps
See `docs/roadmap.md` for detailed tasks on:
- Implementing real client/server transports (WebSocket baseline) and standardized error handling.
- Shipping a runnable server host with configuration, migration/seed commands, and operational docs.
- Wiring authored abilities, enemies, and dungeon templates into the simulation, plus end-to-end network validation.
- Improving developer experience with onboarding guides, playbooks, and observability standards.

If you want a quick start, begin with the transport tasks to unlock end-to-end lobby and chat flows, then stand up the server host against MariaDB before layering in gameplay data.
