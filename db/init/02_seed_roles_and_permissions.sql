-- Seed data for the role/permission catalog.
-- Mirrors architecture doc Section 9 verbatim — both the permission code list
-- ("Full permission catalog" table) and the default-holder assignments per
-- code. Do not add/rename/reassign anything here without updating Section 9
-- first (docs/Schema.md's explicit anti-drift instruction).
-- Roles themselves (lecturer, hod, finance, it, admin) are fixed by the schema.

BEGIN;

-- Roles
INSERT INTO roles (code, default_scope_kind) VALUES
    ('lecturer', 'department'),
    ('hod',      'department'),
    ('finance',  'global'),
    ('it',       'global'),
    ('admin',    'global')
ON CONFLICT (code) DO NOTHING;

-- Permission catalog — the full list from architecture doc Section 9.
INSERT INTO permissions (code, description) VALUES
    ('create_group',                 'Create a community group (TWA-05, AWA-12)'),
    ('create_event',                 'Create a calendar event (TWA-15, AWA-11)'),
    ('add_internal_marks',           'Publish internal marks (TWA-16)'),
    ('add_external_marks',           'Submit external marks (TWA-17) — nobody by default, time-bound PermissionGrant only'),
    ('approve_external_marks',       'Approve external marks (TWA-20)'),
    ('create_timetable',             'Generate/edit the timetable (AWA-01, AWA-03, TWA-19)'),
    ('view_browsing_history',        'Read a student browsing-history summary (AIS-01)'),
    ('manage_fees',                  'Manage fee records and payment links (AWA-04, AWA-05)'),
    ('view_all_fee_records',         'View all fee records'),
    ('manage_accounts',              'Create/manage user accounts (AWA-09)'),
    ('reset_password',               'Reset a user password (AWA-10)'),
    ('manage_roles_and_permissions', 'Assign role bindings and permission grants (AWA-13)'),
    ('manage_departments',           'Create/manage departments (AWA-14)'),
    ('view_all_student_records',     'View all student records (AWA-07)'),
    ('view_all_student_performance', 'View all student performance (AWA-08)'),
    ('view_all_groups',              'View all community groups (TWA-05, AWA-06)'),
    ('view_department_reports',      'View department-level reports')
ON CONFLICT (code) DO NOTHING;

-- Default permission bundles per role, per Section 9's "Default holders" column.
-- Lecturer: department/section-scoped teaching concerns.
INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'lecturer', code FROM permissions
WHERE code IN (
    'create_group', 'create_event',
    'add_internal_marks',
    'view_all_groups'
)
ON CONFLICT DO NOTHING;

-- HoD: everything Lecturer has, plus department-scoped admin duties.
INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'hod', code FROM permissions
WHERE code IN (
    'create_group', 'create_event',
    'add_internal_marks',
    'view_all_groups',
    'create_timetable', 'approve_external_marks', 'view_department_reports'
)
ON CONFLICT DO NOTHING;

-- Finance: fee management, global scope.
INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'finance', code FROM permissions
WHERE code IN (
    'manage_fees', 'view_all_fee_records'
)
ON CONFLICT DO NOTHING;

-- IT: account/role/permission administration, global scope.
INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'it', code FROM permissions
WHERE code IN (
    'manage_accounts', 'reset_password', 'manage_roles_and_permissions'
)
ON CONFLICT DO NOTHING;

-- Admin: full permission set, except add_external_marks — Section 9 states
-- that one has no default holders at all, "nobody by default" applying even
-- to Admin; it's only ever granted via a time-bound PermissionGrant per
-- TWA-17's own spec.
INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'admin', code FROM permissions
WHERE code <> 'add_external_marks'
ON CONFLICT DO NOTHING;

COMMIT;
