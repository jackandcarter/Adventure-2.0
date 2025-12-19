CREATE TABLE IF NOT EXISTS abilities (
    ability_id VARCHAR(64) PRIMARY KEY,
    display_name VARCHAR(128) NOT NULL,
    description VARCHAR(512) NULL,
    created_at DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS room_templates (
    template_id VARCHAR(64) PRIMARY KEY,
    display_name VARCHAR(128) NOT NULL,
    template_type VARCHAR(64) NOT NULL,
    created_at DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
