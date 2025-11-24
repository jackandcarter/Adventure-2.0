# Adventure 2.0 Delivery Roadmap

This roadmap translates the current high-level status into concrete, buildable tasks for the next milestones. It focuses on three areas that are close to completion but still need implementation detail: networking transport, runnable server operations, and gameplay/content wiring.

## 1) Networking transport (client + server)
- **Transport selection and envelope framing**
  - Decide on TCP vs WebSocket for Unity; baseline is WebSocket (TLS-capable, browser-friendly) using `ClientMessagePipeline` envelopes.
  - Lock envelope framing: JSON for parity with existing DTOs, with gzip/deflate optional if payload size becomes an issue.
- **Server listener and session validation path**
  - Add a listener service that accepts connections, reads envelopes, validates session tokens via `SessionManager`, and forwards to `MessageRouter`.
  - Maintain a connection registry keyed by session; implement heartbeat/timeout rules mirroring client expectations.
- **Client transport adapter**
  - Implement a `WebSocketTransport` that plugs into `NetworkClient`, handling connect/reconnect, send queue flushing, and on-message dispatch into `ClientMessagePipeline`.
  - Surface reconnect/backoff settings (initial delay, max delay, jitter) as serialized Unity settings for designers to tune.
- **Reliability and error handling**
  - Standardize disconnect reasons and error envelopes so both sides emit actionable UI events (e.g., auth expired, server shutdown, version mismatch).
  - Add integration tests that spin up a minimal echo server to validate handshake, heartbeats, reconnects, and backpressure behavior.

## 2) Runnable server entrypoints and operations
- **Host composition**
  - Create a server entrypoint that wires repositories (MariaDB) and in-memory services (sessions, parties, chat) into a single host with dependency injection.
  - Provide configuration via environment variables: DB credentials, listen address/port, tick rate, auth secret, and logging level.
- **Migration and bootstrap**
  - Add a CLI verb to run DB migrations located in `db/migrations` before launch; fail fast on schema drift.
  - Seed minimal reference data (default abilities/rooms) if tables are empty to support smoke tests.
- **Runtime lifecycle**
  - Implement graceful shutdown hooks (SIGTERM handling) that stop the simulation loop, flush run logs, and close transports cleanly.
  - Expose health/readiness probes (HTTP or socket ping) for container orchestration.
- **Ops documentation**
  - Author a "Run the server locally" guide that covers prerequisites (MariaDB), configuring env vars, running migrations, and starting the host.
  - Add a troubleshooting section for common failures (DB auth, port binding, migration version mismatch).

## 3) Gameplay/content wiring and validation
- **Data-driven ability and enemy catalogs**
  - Define a canonical JSON/YAML schema for abilities, enemy archetypes, and loot tables; co-locate sample data under `Assets/Data` or `Server/Simulation/Data`.
  - Extend the simulation layer to load these definitions at startup and register them with combat/AI systems.
- **Dungeon templates and room hooks**
  - Expand `DungeonGenerationManager` inputs to accept authored room templates (doors, keys, triggers) and emit them to the server for authoritative validation.
  - Add server-side validation ensuring client-sent room interactions align with template constraints (e.g., key possession).
- **End-to-end simulation events**
  - Wire network events for movement, ability casts, and damage into the tick loop so that authoritative results propagate back to clients via the existing DTOs.
  - Add deterministic replay/logging for dungeon runs to aid debugging and combat balance.
- **QA and test coverage**
  - Create golden-path integration tests: login → lobby → party create/join → dungeon start → first room clear.
  - Add load-test scaffolding (locust/k6) to validate lobby chat and heartbeat scalability.

## 4) Documentation and developer experience
- **Developer onboarding**
  - Expand README with project overview, repo layout, and links to setup/run guides.
  - Provide "playbooks" for common workflows: adding a new DTO, extending a repository, authoring a room template, and capturing network traces.
- **Observability**
  - Standardize structured logging fields (session, character, party, dungeonRunId) and propose metrics (active sessions, party creations, avg tick duration).
  - Document log levels and sampling for chat vs combat spam to keep production logs usable.

---

This plan keeps transport, server hosting, and gameplay content on parallel tracks while creating the documentation and test scaffolding needed for rapid iteration.
