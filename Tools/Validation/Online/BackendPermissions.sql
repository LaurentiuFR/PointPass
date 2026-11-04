-- Run as the limited pointpassapi role, against pointpass_validation only.
DO $$
BEGIN
    IF current_database() <> 'pointpass_validation' OR current_user <> 'pointpassapi' THEN
        RAISE EXCEPTION 'This check requires the disposable validation database and application role';
    END IF;
    IF has_schema_privilege(current_user, 'public', 'CREATE') THEN
        RAISE EXCEPTION 'Application role unexpectedly has schema CREATE privilege';
    END IF;
    IF has_table_privilege(current_user, 'schema_migrations', 'UPDATE') THEN
        RAISE EXCEPTION 'Application role unexpectedly can alter the migration ledger';
    END IF;
    IF pg_has_role(current_user, 'pointpassowner', 'MEMBER') THEN
        RAISE EXCEPTION 'Application role unexpectedly belongs to the deployment owner';
    END IF;
    IF NOT has_table_privilege(current_user, 'players', 'SELECT,INSERT,UPDATE,DELETE') THEN
        RAISE EXCEPTION 'Application role lacks required player data access';
    END IF;
END;
$$;
SELECT 'PASS limited role: no schema creation, migration mutation or owner membership; required data CRUD allowed' AS verification;
