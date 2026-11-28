-- One-time: gives the API's managed identity its own user in the environment's
-- database, so it can sign in with Entra and apply the EF migrations at startup.
-- Run it as a member of the SQL admin group (the server is Entra-only), against
-- the database itself (sqldb-dip-dev), not master. Safe to run again.
--
-- Easiest: Azure portal > SQL database sqldb-dip-dev > Query editor, sign in with
-- Entra, allow your IP when asked, paste this, Run.
--
-- For demo, replace id-dip-dev-api with id-dip-demo-api.

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'id-dip-dev-api')
    CREATE USER [id-dip-dev-api] FROM EXTERNAL PROVIDER;

-- Read and write rows; db_ddladmin lets it create and change tables through the
-- migrations (decision log, M1: revisit before M8, when migrations could move to
-- a separate job with its own identity).
ALTER ROLE db_datareader ADD MEMBER [id-dip-dev-api];
ALTER ROLE db_datawriter ADD MEMBER [id-dip-dev-api];
ALTER ROLE db_ddladmin ADD MEMBER [id-dip-dev-api];

-- Check: should list the three roles.
SELECT r.name AS role_name
FROM sys.database_role_members m
JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
JOIN sys.database_principals u ON u.principal_id = m.member_principal_id
WHERE u.name = 'id-dip-dev-api';
