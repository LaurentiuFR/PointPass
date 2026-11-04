#!/usr/bin/env bash
# Opt-in disposable integration database; never points tests at live player data.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
runuser -u postgres -- psql -Xv ON_ERROR_STOP=1 postgres <<'SQL'
SELECT 'CREATE DATABASE pointpass_validation OWNER pointpassowner'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pointpass_validation') \gexec
REVOKE ALL ON DATABASE pointpass_validation FROM PUBLIC;
GRANT CONNECT ON DATABASE pointpass_validation TO pointpassapi;
SQL
owner=$(runuser -u postgres -- psql -XAtqc "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='pointpass_validation'")
[[ $owner == pointpassowner ]] || { echo 'Unexpected validation database owner; stop for review.' >&2; exit 1; }
runuser -u postgres -- psql -Xv ON_ERROR_STOP=1 pointpass_validation <<'SQL'
REVOKE ALL ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO pointpassowner;
GRANT USAGE ON SCHEMA public TO pointpassapi;
ALTER DEFAULT PRIVILEGES FOR ROLE pointpassowner IN SCHEMA public GRANT SELECT,INSERT,UPDATE,DELETE ON TABLES TO pointpassapi;
ALTER DEFAULT PRIVILEGES FOR ROLE pointpassowner IN SCHEMA public GRANT USAGE,SELECT ON SEQUENCES TO pointpassapi;
SQL
hba=/etc/postgresql/18/main/pg_hba.conf
if ! grep -Eq '^local[[:space:]]+pointpass_validation[[:space:]]+pointpassapi[[:space:]]+peer$' "$hba"; then
  temporary=$(mktemp /etc/postgresql/18/main/pg_hba.validation.XXXXXX)
  trap '[[ ! -e $temporary ]] || unlink "$temporary"' EXIT
  awk '/^local all all reject$/ { print "local pointpass_validation pointpassapi peer" } { print }' "$hba" > "$temporary"
  chown postgres:postgres "$temporary"
  chmod 0640 "$temporary"
  mv -- "$temporary" "$hba"
fi
systemctl reload postgresql@18-main
runuser -u pointpassapi -- psql -XAtqc 'SELECT current_database()' pointpass_validation
echo 'Isolated validation database ready. Test API must use loopback5001 and this exact _validation database.'
