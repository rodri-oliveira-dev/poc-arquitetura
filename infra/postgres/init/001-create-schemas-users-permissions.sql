\set ON_ERROR_STOP on

\set ledger_db_password `printf '%s' "${LEDGER_DB_PASSWORD:?Defina LEDGER_DB_PASSWORD}"`
\set ledger_db_migrator_password `printf '%s' "${LEDGER_DB_MIGRATOR_PASSWORD:?Defina LEDGER_DB_MIGRATOR_PASSWORD}"`
\set balance_db_read_password `printf '%s' "${BALANCE_DB_READ_PASSWORD:?Defina BALANCE_DB_READ_PASSWORD}"`
\set balance_db_write_password `printf '%s' "${BALANCE_DB_WRITE_PASSWORD:?Defina BALANCE_DB_WRITE_PASSWORD}"`
\set balance_db_migrator_password `printf '%s' "${BALANCE_DB_MIGRATOR_PASSWORD:?Defina BALANCE_DB_MIGRATOR_PASSWORD}"`
\set transfer_db_password `printf '%s' "${TRANSFER_DB_PASSWORD:?Defina TRANSFER_DB_PASSWORD}"`
\set transfer_db_migrator_password `printf '%s' "${TRANSFER_DB_MIGRATOR_PASSWORD:?Defina TRANSFER_DB_MIGRATOR_PASSWORD}"`
\set payment_db_password `printf '%s' "${PAYMENT_DB_PASSWORD:?Defina PAYMENT_DB_PASSWORD}"`
\set payment_db_migrator_password `printf '%s' "${PAYMENT_DB_MIGRATOR_PASSWORD:?Defina PAYMENT_DB_MIGRATOR_PASSWORD}"`
\set identity_db_password `printf '%s' "${IDENTITY_DB_PASSWORD:?Defina IDENTITY_DB_PASSWORD}"`
\set identity_db_migrator_password `printf '%s' "${IDENTITY_DB_MIGRATOR_PASSWORD:?Defina IDENTITY_DB_MIGRATOR_PASSWORD}"`
\set identity_app_role 'identity_app_user'
\set identity_migrator_role 'identity_migrator_user'

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

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'transfer_app_user', :'transfer_db_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'transfer_app_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'transfer_app_user', :'transfer_db_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'transfer_migrator_user', :'transfer_db_migrator_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'transfer_migrator_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'transfer_migrator_user', :'transfer_db_migrator_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'payment_app_user', :'payment_db_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'payment_app_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'payment_app_user', :'payment_db_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'payment_migrator_user', :'payment_db_migrator_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'payment_migrator_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', 'payment_migrator_user', :'payment_db_migrator_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', :'identity_app_role', :'identity_db_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = :'identity_app_role') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', :'identity_app_role', :'identity_db_password') \gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', :'identity_migrator_role', :'identity_db_migrator_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = :'identity_migrator_role') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION', :'identity_migrator_role', :'identity_db_migrator_password') \gexec

CREATE SCHEMA IF NOT EXISTS ledger AUTHORIZATION ledger_migrator_user;
CREATE SCHEMA IF NOT EXISTS balance AUTHORIZATION balance_migrator_user;
CREATE SCHEMA IF NOT EXISTS transfer AUTHORIZATION transfer_migrator_user;
CREATE SCHEMA IF NOT EXISTS payment AUTHORIZATION payment_migrator_user;
CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION identity_migrator_user;

ALTER SCHEMA ledger OWNER TO ledger_migrator_user;
ALTER SCHEMA balance OWNER TO balance_migrator_user;
ALTER SCHEMA transfer OWNER TO transfer_migrator_user;
ALTER SCHEMA payment OWNER TO payment_migrator_user;
ALTER SCHEMA identity OWNER TO identity_migrator_user;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;

ALTER ROLE ledger_app_user SET search_path = ledger;
ALTER ROLE ledger_migrator_user SET search_path = ledger;
ALTER ROLE balance_read_user SET search_path = balance;
ALTER ROLE balance_write_user SET search_path = balance;
ALTER ROLE balance_migrator_user SET search_path = balance;
ALTER ROLE transfer_app_user SET search_path = transfer;
ALTER ROLE transfer_migrator_user SET search_path = transfer;
ALTER ROLE payment_app_user SET search_path = payment;
ALTER ROLE payment_migrator_user SET search_path = payment;
ALTER ROLE identity_app_user SET search_path = identity;
ALTER ROLE identity_migrator_user SET search_path = identity;

REVOKE ALL ON SCHEMA ledger FROM PUBLIC;
REVOKE ALL ON SCHEMA balance FROM PUBLIC;
REVOKE ALL ON SCHEMA transfer FROM PUBLIC;
REVOKE ALL ON SCHEMA payment FROM PUBLIC;
REVOKE ALL ON SCHEMA identity FROM PUBLIC;

REVOKE ALL ON SCHEMA ledger FROM balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL ON SCHEMA balance FROM ledger_app_user, ledger_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL ON SCHEMA transfer FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL ON SCHEMA payment FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user;
REVOKE ALL ON SCHEMA identity FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;

GRANT USAGE ON SCHEMA ledger TO ledger_app_user;
GRANT USAGE, CREATE ON SCHEMA ledger TO ledger_migrator_user;

GRANT USAGE ON SCHEMA balance TO balance_read_user, balance_write_user;
GRANT USAGE, CREATE ON SCHEMA balance TO balance_migrator_user;
GRANT USAGE ON SCHEMA transfer TO transfer_app_user;
GRANT USAGE, CREATE ON SCHEMA transfer TO transfer_migrator_user;
GRANT USAGE ON SCHEMA payment TO payment_app_user;
GRANT USAGE, CREATE ON SCHEMA payment TO payment_migrator_user;
GRANT USAGE ON SCHEMA identity TO identity_app_user;
GRANT USAGE, CREATE ON SCHEMA identity TO identity_migrator_user;

REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA ledger FROM balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA ledger FROM balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA balance FROM ledger_app_user, ledger_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA balance FROM ledger_app_user, ledger_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA transfer FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA transfer FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA payment FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA payment FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user;
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA identity FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA identity FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA ledger TO ledger_app_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA ledger TO ledger_app_user;

GRANT SELECT ON ALL TABLES IN SCHEMA balance TO balance_read_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA balance TO balance_write_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA balance TO balance_write_user;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA transfer TO transfer_app_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA transfer TO transfer_app_user;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA payment TO payment_app_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA payment TO payment_app_user;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO identity_app_user;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA identity TO identity_app_user;

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

ALTER DEFAULT PRIVILEGES FOR ROLE transfer_migrator_user IN SCHEMA transfer
    REVOKE ALL ON TABLES FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE transfer_migrator_user IN SCHEMA transfer
    REVOKE ALL ON SEQUENCES FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE transfer_migrator_user IN SCHEMA transfer
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO transfer_app_user;
ALTER DEFAULT PRIVILEGES FOR ROLE transfer_migrator_user IN SCHEMA transfer
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO transfer_app_user;

ALTER DEFAULT PRIVILEGES FOR ROLE payment_migrator_user IN SCHEMA payment
    REVOKE ALL ON TABLES FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE payment_migrator_user IN SCHEMA payment
    REVOKE ALL ON SEQUENCES FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE payment_migrator_user IN SCHEMA payment
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO payment_app_user;
ALTER DEFAULT PRIVILEGES FOR ROLE payment_migrator_user IN SCHEMA payment
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO payment_app_user;

ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator_user IN SCHEMA identity
    REVOKE ALL ON TABLES FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator_user IN SCHEMA identity
    REVOKE ALL ON SEQUENCES FROM ledger_app_user, ledger_migrator_user, balance_read_user, balance_write_user, balance_migrator_user, transfer_app_user, transfer_migrator_user, payment_app_user, payment_migrator_user;
ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator_user IN SCHEMA identity
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO identity_app_user;
ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator_user IN SCHEMA identity
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO identity_app_user;
