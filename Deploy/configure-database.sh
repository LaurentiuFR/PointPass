#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
[[ -f /etc/postgresql/18/main/postgresql.conf ]] || { echo 'PostgreSQL 18 main cluster missing.' >&2; exit 1; }
[[ -d /var/lib/pointpass-deploy ]] || { echo 'Run install-base.sh first.' >&2; exit 1; }

# This script owns the fresh pilot cluster; refuse an unrelated existing database.
unrelated=$(runuser -u postgres -- psql -XAtqc "SELECT datname FROM pg_database WHERE datname NOT IN ('postgres','template0','template1','pointpass','pointpass_validation') AND datname NOT LIKE 'pointpass_restore_%'")
[[ -z $unrelated ]] || { echo 'Unrelated database exists; review cluster before changing configuration.' >&2; exit 1; }
install -d -m 0755 /etc/postgresql/18/main/conf.d
cat > /etc/postgresql/18/main/conf.d/pointpass.conf <<'EOF'
# Private socket-only pilot database. No TCP listener, including loopback.
listen_addresses = ''
unix_socket_directories = '/var/run/postgresql'
max_connections = 24
shared_buffers = '128MB'
work_mem = '4MB'
maintenance_work_mem = '64MB'
idle_in_transaction_session_timeout = '15s'
statement_timeout = '10s'
log_statement = 'none'
log_min_duration_statement = -1
logging_collector = off
EOF
chown root:postgres /etc/postgresql/18/main/conf.d/pointpass.conf
chmod 0640 /etc/postgresql/18/main/conf.d/pointpass.conf
if [[ ! -f /var/lib/pointpass-deploy/pg_hba.conf.before ]]; then
  install -m 0600 /etc/postgresql/18/main/pg_hba.conf /var/lib/pointpass-deploy/pg_hba.conf.before
fi
cat > /etc/postgresql/18/main/pg_hba.conf <<'EOF'
local all postgres peer
local pointpass pointpassapi peer
local all all reject
host all all 0.0.0.0/0 reject
host all all ::/0 reject
EOF
chown postgres:postgres /etc/postgresql/18/main/pg_hba.conf
chmod 0640 /etc/postgresql/18/main/pg_hba.conf
systemctl restart postgresql@18-main
runuser -u postgres -- psql -Xv ON_ERROR_STOP=1 postgres <<'SQL'
SELECT 'CREATE ROLE pointpassowner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'pointpassowner') \gexec
SELECT 'CREATE ROLE pointpassapi LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION CONNECTION LIMIT 8'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'pointpassapi') \gexec
SELECT 'CREATE DATABASE pointpass OWNER pointpassowner'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pointpass') \gexec
REVOKE ALL ON DATABASE pointpass FROM PUBLIC;
GRANT CONNECT ON DATABASE pointpass TO pointpassapi;
SQL
runuser -u postgres -- psql -Xv ON_ERROR_STOP=1 pointpass <<'SQL'
REVOKE ALL ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO pointpassowner;
GRANT USAGE ON SCHEMA public TO pointpassapi;
ALTER DEFAULT PRIVILEGES FOR ROLE pointpassowner IN SCHEMA public GRANT SELECT,INSERT,UPDATE,DELETE ON TABLES TO pointpassapi;
ALTER DEFAULT PRIVILEGES FOR ROLE pointpassowner IN SCHEMA public GRANT USAGE,SELECT ON SEQUENCES TO pointpassapi;
SQL
install -d -m 0755 /etc/systemd/system/postgresql@18-main.service.d
cat > /etc/systemd/system/postgresql@18-main.service.d/pointpass-limits.conf <<'EOF'
[Service]
MemoryHigh=640M
MemoryMax=768M
CPUQuota=100%
TasksMax=128
EOF
systemctl daemon-reload
systemctl restart postgresql@18-main
echo 'PostgreSQL is socket-only; runtime role has no DDL/admin privilege. Apply versioned migration during deployment.'
