CREATE TABLE IF NOT EXISTS dungeon_runs (
    run_id VARCHAR(64) PRIMARY KEY,
    dungeon_id VARCHAR(64) NOT NULL,
    party_id VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    started_at DATETIME NOT NULL,
    ended_at DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE INDEX IF NOT EXISTS idx_dungeon_runs_dungeon ON dungeon_runs(dungeon_id);
