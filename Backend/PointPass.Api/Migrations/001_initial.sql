-- Run once as the deployment owner, inside a transaction. Runtime role has no DDL privileges.
CREATE TABLE IF NOT EXISTS schema_migrations (version integer PRIMARY KEY, applied_utc timestamptz NOT NULL DEFAULT now());
CREATE TABLE players (
    id uuid PRIMARY KEY,
    credential_hash bytea NOT NULL UNIQUE CHECK (octet_length(credential_hash) = 32),
    display_name varchar(24) NOT NULL,
    created_utc timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE invitations (
    id uuid PRIMARY KEY,
    code_hash bytea NOT NULL UNIQUE CHECK (octet_length(code_hash) = 32),
    expires_utc timestamptz NOT NULL,
    maximum_uses integer NOT NULL DEFAULT 1 CHECK (maximum_uses BETWEEN 1 AND 10),
    used integer NOT NULL DEFAULT 0 CHECK (used >= 0)
);
CREATE TABLE access_sessions (
    token_hash bytea PRIMARY KEY CHECK (octet_length(token_hash) = 32),
    player_id uuid NOT NULL REFERENCES players(id),
    created_utc timestamptz NOT NULL DEFAULT now(),
    expires_utc timestamptz NOT NULL
);
CREATE INDEX access_sessions_player ON access_sessions(player_id, expires_utc);
CREATE TABLE workers (
    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
    instance_id uuid NOT NULL,
    game_version varchar(64) NOT NULL,
    heartbeat_utc timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE matches (
    id uuid PRIMARY KEY,
    worker_instance uuid NOT NULL,
    game_version varchar(64) NOT NULL,
    state varchar(12) NOT NULL CHECK (state IN ('waiting', 'active', 'completed', 'abandoned')),
    created_utc timestamptz NOT NULL DEFAULT now(),
    started_utc timestamptz,
    finished_utc timestamptz,
    blue_score integer CHECK (blue_score BETWEEN 0 AND 999),
    red_score integer CHECK (red_score BETWEEN 0 AND 999),
    finish_reason varchar(32)
);
CREATE UNIQUE INDEX one_open_match ON matches ((true)) WHERE state IN ('waiting', 'active');
CREATE TABLE match_players (
    match_id uuid NOT NULL REFERENCES matches(id),
    player_id uuid NOT NULL REFERENCES players(id),
    slot smallint NOT NULL CHECK (slot IN (0, 1)),
    connected boolean NOT NULL DEFAULT false,
    ever_connected boolean NOT NULL DEFAULT false,
    reserved_until timestamptz NOT NULL,
    disconnected_utc timestamptz,
    connection_id varchar(24),
    PRIMARY KEY (match_id, player_id),
    UNIQUE (match_id, slot)
);
CREATE INDEX match_players_history ON match_players(player_id, match_id);
CREATE TABLE match_tickets (
    token_hash bytea PRIMARY KEY CHECK (octet_length(token_hash) = 32),
    match_id uuid NOT NULL,
    player_id uuid NOT NULL,
    worker_instance uuid NOT NULL,
    game_version varchar(64) NOT NULL,
    expires_utc timestamptz NOT NULL,
    used_utc timestamptz,
    FOREIGN KEY (match_id, player_id) REFERENCES match_players(match_id, player_id) ON DELETE CASCADE
);
CREATE INDEX match_tickets_expiry ON match_tickets(expires_utc);
INSERT INTO schema_migrations(version) VALUES (1);
-- Default table grants apply to gameplay data; migration bookkeeping stays private.
REVOKE ALL ON schema_migrations FROM pointpassapi;
