CREATE TABLE IF NOT EXISTS bff_user (
    id text PRIMARY KEY,
    name text NOT NULL,
    email text NOT NULL UNIQUE,
    email_verified boolean NOT NULL DEFAULT false,
    image text NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS bff_session (
    id text PRIMARY KEY,
    expires_at timestamptz NOT NULL,
    token text NOT NULL UNIQUE,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    ip_address text NULL,
    user_agent text NULL,
    user_id text NOT NULL REFERENCES bff_user(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_bff_session_user_id ON bff_session(user_id);
CREATE INDEX IF NOT EXISTS ix_bff_session_expires_at ON bff_session(expires_at);

CREATE TABLE IF NOT EXISTS bff_account (
    id text PRIMARY KEY,
    account_id text NOT NULL,
    provider_id text NOT NULL,
    user_id text NOT NULL REFERENCES bff_user(id) ON DELETE CASCADE,
    access_token text NULL,
    refresh_token text NULL,
    id_token text NULL,
    access_token_expires_at timestamptz NULL,
    refresh_token_expires_at timestamptz NULL,
    scope text NULL,
    password text NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT uq_bff_account_provider_subject UNIQUE (provider_id, account_id)
);

CREATE INDEX IF NOT EXISTS ix_bff_account_user_id ON bff_account(user_id);

CREATE TABLE IF NOT EXISTS bff_verification (
    id text PRIMARY KEY,
    identifier text NOT NULL,
    value text NOT NULL,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_bff_verification_identifier ON bff_verification(identifier);
CREATE INDEX IF NOT EXISTS ix_bff_verification_expires_at ON bff_verification(expires_at);

CREATE TABLE IF NOT EXISTS bff_rate_limit (
    id text PRIMARY KEY,
    key text NOT NULL UNIQUE,
    count integer NOT NULL,
    last_request bigint NOT NULL
);

ALTER TABLE bff_rate_limit ADD COLUMN IF NOT EXISTS id text;
UPDATE bff_rate_limit SET id = key WHERE id IS NULL;
ALTER TABLE bff_rate_limit ALTER COLUMN id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_bff_rate_limit_id ON bff_rate_limit(id);
