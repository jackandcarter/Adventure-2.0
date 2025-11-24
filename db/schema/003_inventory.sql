CREATE TABLE IF NOT EXISTS inventory (
    inventory_item_id VARCHAR(64) PRIMARY KEY,
    character_id VARCHAR(64) NOT NULL,
    slot VARCHAR(64) NOT NULL,
    item_definition_id VARCHAR(128) NOT NULL,
    quantity INT NOT NULL,
    acquired_at DATETIME NOT NULL,
    CONSTRAINT fk_inventory_character FOREIGN KEY (character_id) REFERENCES characters(character_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE INDEX IF NOT EXISTS idx_inventory_character ON inventory(character_id);
