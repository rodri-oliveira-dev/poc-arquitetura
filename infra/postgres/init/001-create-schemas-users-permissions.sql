\set ON_ERROR_STOP on

\set ledger_db_password `printf '%s' "${LEDGER_DB_PASSWORD:-local_dev_password}"`
\set ledger_db_migrator_password `printf '%s' "${LEDGER_DB_MIGRATOR_PASSWORD:-local_dev_password}"`
\set balance_db_read_password `printf '%s' "${BALANCE_DB_READ_PASSWORD:-local_dev_password}"`
\set balance_db_write_password `printf '%s' "${BALANCE_DB_WRITE_PASSWORD:-local_dev_password}"`
\set balance_db_migrator_password `printf '%s' "${BALANCE_DB_MIGRATOR_PASSWORD:-local_dev_password}"`

-- Idempotent local bootstrap for the shared PostgreSQL container.
-- Runtime roles receive only DML privileges in their own service schema.
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'ledger_app_user', :'ledger_db_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'ledger_app_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'ledger_app_user', :'ledger_db_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'ledger_migrator_user', :'ledger_db_migrator_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'ledger_migrator_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'ledger_migrator_user', :'ledger_db_migrator_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'balance_read_user', :'balance_db_read_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'balance_read_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'balance_read_user', :'balance_db_read_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'balance_write_user', :'balance_db_write_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'balance_write_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'balance_write_user', :'balance_db_write_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'balance_migrator_user', :'balance_db_migrator_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'balance_migrator_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'balance_migrator_user', :'balance_db_migrator_password') \gexec

CREATE SCHEMA IF NOT EXISTS ledger AUTHORIZATION ledger_migrator_user;
CREATE SCHEMA IF NOT EXISTS balance AUTHORIZATION balance_migrator_user;

ALTER SCHEMA ledger OWNER TO ledger_migrator_user;
ALTER SCHEMA balance OWNER TO balance_migrator_user;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;

ALTER ROLE ledger_app_user SET search_path = ledger;
ALTER ROLE ledger_migrator_user SET search_path = ledger;
ALTER ROLE balance_read_user SET search_path = balance;
ALTER ROLE balance_write_user SET search_path = balance;
ALTER ROLE balance_migrator_user SET search_path = balance;

REVOKE ALL ON SCHEMA ledger FROM PUBLIC;
REVOKE ALL ON SCHEMA balance FROM PUBLIC;

REVOKE ALL ON SCHEMA ledger FROM balance_read_user, balance_write_user, balance_migrator_user;
REVOKE ALL ON SCHEMA balance FROM ledger_app_user, ledger_migrator_user;

GRANT USAGE ON SCHEMA ledger TO ledger_app_user;
GRANT USAGE, CREATE ON SCHEMA ledger TO ledger_migrator_user;

GRANT USAGE ON SCHEMA balance TO balance_read_user, balance_write_user;
GRANT USAGE, CREATE ON SCHEMA balance TO balance_migrator_user;

REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA ledger FROM balance_read_user, balance_write_user, balance_migrator_user;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA ledger FROM balance_read_user, balance_write_user, balance_migrator_user;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA balance FROM ledger_app_user, ledger_migrator_user;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA balance FROM ledger_app_user, ledger_migrator_user;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA ledger TO ledger_app_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA ledger TO ledger_app_user;

GRANT SELECT ON ALL TABLES IN SCHEMA balance TO balance_read_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA balance TO balance_write_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA balance TO balance_write_user;

ALTER DEFAULT PRIVILEGES FOR ROLE ledger_migrator_user IN SCHEMA ledger
    REVOKE ALL ON TABLES FROM balance_read_user, balance_write_user, balance_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE ledger_migrator_user IN SCHEMA ledger
    REVOKE ALL ON SEQUENCES FROM balance_read_user, balance_write_user, balance_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE ledger_migrator_user IN SCHEMA ledger
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ledger_app_user;
ALTER DEFAULT PRIVILEGES FOR ROLE ledger_migrator_user IN SCHEMA ledger
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO ledger_app_user;

ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
    REVOKE ALL ON TABLES FROM ledger_app_user, ledger_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
    REVOKE ALL ON SEQUENCES FROM ledger_app_user, ledger_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
    GRANT SELECT ON TABLES TO balance_read_user;
ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO balance_write_user;
ALTER DEFAULT PRIVILEGES FOR ROLE balance_migrator_user IN SCHEMA balance
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO balance_write_user;
