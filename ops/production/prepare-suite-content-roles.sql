\set ON_ERROR_STOP on
DO $$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-content-api') THEN
    EXECUTE 'CREATE ROLE "turborama-suite-content-api" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-gateway') THEN
    EXECUTE 'CREATE ROLE "turborama-suite-gateway" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-publisher') THEN
    EXECUTE 'CREATE ROLE "turborama-suite-publisher" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-content-admin') THEN
    EXECUTE 'CREATE ROLE "turborama-suite-content-admin" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-content-monitor') THEN
    EXECUTE 'CREATE ROLE "turborama-suite-content-monitor" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-content-maintenance') THEN
    EXECUTE 'CREATE ROLE "turborama-suite-content-maintenance" LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION';
  END IF;
END $$;
ALTER ROLE "turborama-suite-content-api" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
ALTER ROLE "turborama-suite-gateway" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
ALTER ROLE "turborama-suite-publisher" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
ALTER ROLE "turborama-suite-content-admin" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
ALTER ROLE "turborama-suite-content-monitor" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
ALTER ROLE "turborama-suite-content-maintenance" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;

DO $$
DECLARE role_names text[] := ARRAY[
  'turborama-suite-content-api','turborama-suite-gateway','turborama-suite-publisher',
  'turborama-suite-content-admin','turborama-suite-content-monitor',
  'turborama-suite-content-maintenance'];
BEGIN
  IF EXISTS(SELECT 1 FROM pg_roles role
    WHERE role.rolname=ANY(role_names) AND
      (NOT role.rolcanlogin OR role.rolinherit OR role.rolsuper OR role.rolbypassrls OR
       role.rolcreatedb OR role.rolcreaterole OR role.rolreplication)) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_ROLE_ATTRIBUTES_UNSAFE';
  END IF;
  IF EXISTS(SELECT 1 FROM pg_auth_members membership
    JOIN pg_roles member_role ON member_role.oid=membership.member
    JOIN pg_roles granted_role ON granted_role.oid=membership.roleid
    WHERE member_role.rolname=ANY(role_names)
       OR granted_role.rolname=ANY(role_names)) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_ROLE_MEMBERSHIP_UNEXPECTED';
  END IF;
END $$;
ALTER ROLE "turborama-suite-content-api" SET statement_timeout='30s';
ALTER ROLE "turborama-suite-content-api" SET lock_timeout='5s';
ALTER ROLE "turborama-suite-gateway" SET statement_timeout='30s';
ALTER ROLE "turborama-suite-gateway" SET lock_timeout='5s';
ALTER ROLE "turborama-suite-publisher" SET statement_timeout='120s';
ALTER ROLE "turborama-suite-publisher" SET lock_timeout='5s';
ALTER ROLE "turborama-suite-content-admin" SET statement_timeout='30s';
ALTER ROLE "turborama-suite-content-admin" SET lock_timeout='5s';
ALTER ROLE "turborama-suite-content-monitor" SET statement_timeout='120s';
ALTER ROLE "turborama-suite-content-monitor" SET lock_timeout='5s';
ALTER ROLE "turborama-suite-content-maintenance" SET statement_timeout='300s';
ALTER ROLE "turborama-suite-content-maintenance" SET lock_timeout='5s';
