CREATE TABLE IF NOT EXISTS unlocks (
    unlock_id VARCHAR(64) PRIMARY KEY,
    account_id VARCHAR(64) NOT NULL,
    unlock_key VARCHAR(128) NOT NULL,
    unlocked_at DATETIME NOT NULL,
    CONSTRAINT fk_unlocks_account FOREIGN KEY (account_id) REFERENCES accounts(account_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE INDEX IF NOT EXISTS idx_unlocks_account ON unlocks(account_id);
