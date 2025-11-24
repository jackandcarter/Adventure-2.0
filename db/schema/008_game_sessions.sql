CREATE TABLE IF NOT EXISTS game_sessions (
    session_id VARCHAR(64) PRIMARY KEY,
    owner_account_id VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL,
    dungeon_id VARCHAR(64) NULL,
    saved_state_json TEXT NULL,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL,
    CONSTRAINT fk_game_sessions_owner FOREIGN KEY (owner_account_id) REFERENCES accounts(account_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS game_session_members (
    session_id VARCHAR(64) NOT NULL,
    account_id VARCHAR(64) NOT NULL,
    join_order INT NOT NULL,
    joined_at DATETIME NOT NULL,
    PRIMARY KEY(session_id, account_id),
    CONSTRAINT fk_game_session_members_session FOREIGN KEY (session_id) REFERENCES game_sessions(session_id) ON DELETE CASCADE,
    CONSTRAINT fk_game_session_members_account FOREIGN KEY (account_id) REFERENCES accounts(account_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
