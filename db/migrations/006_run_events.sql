CREATE TABLE IF NOT EXISTS run_events (
    event_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    run_id VARCHAR(64) NOT NULL,
    event_type VARCHAR(64) NOT NULL,
    payload_json JSON NOT NULL,
    occurred_at DATETIME NOT NULL,
    CONSTRAINT fk_run_events_run FOREIGN KEY (run_id) REFERENCES dungeon_runs(run_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE INDEX IF NOT EXISTS idx_run_events_run ON run_events(run_id);
