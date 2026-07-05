-- Seed data for the role/permission catalog.
-- The full permission-code list comes from architecture doc Section 9.
-- The role/permission mapping here is a sensible default — adjust per
-- your institution's policy. Roles themselves (lecturer, hod, finance, it, admin)
-- are fixed by the schema.

BEGIN;

-- Roles
INSERT INTO roles (code, default_scope_kind) VALUES
    ('lecturer', 'department'),
    ('hod',      'department'),
    ('finance',  'global'),
    ('it',       'global'),
    ('admin',    'global')
ON CONFLICT (code) DO NOTHING;

-- Permission catalog (subset aligned with features in the schema/architecture doc).
-- Add more as Section 9 expands.
INSERT INTO permissions (code, description) VALUES
    -- Timetable
    ('create_timetable',           'Generate the timetable via AWA-01/02'),
    ('edit_timetable_slot',        'Patch a single timetable slot (AWA-03 manual override)'),
    ('review_timetable_request',   'Approve/reject TWA-13 change requests'),
    -- Attendance
    ('mark_attendance',            'Mark student attendance for a class session (TWA-08)'),
    ('view_attendance_alerts',     'See absence alerts (TWA-09)'),
    -- Assignments / submissions
    ('create_assignment',          'Create an assignment (TWA-07)'),
    ('grade_submission',           'Confirm/edit an autograde suggestion'),
    ('view_plagiarism_report',     'Read Copyleaks report (AIS-02)'),
    ('view_copy_check',            'Read cross-submission copy check (AIS-03)'),
    ('view_ai_detection',          'Read Pangram AI-detection report (AIS-05)'),
    -- Marks
    ('add_internal_marks',         'Publish internal marks (TWA-16)'),
    ('add_external_marks',         'Submit external marks (TWA-17)'),
    ('approve_external_marks',     'Approve external marks (TWA-20)'),
    -- Community / calendar
    ('create_group',               'Create a community group (TWA-05, AWA-12)'),
    ('create_event',               'Create a calendar event (TWA-15, AWA-11)'),
    -- Admin / tenancy
    ('create_user',                'Create a user (AWA-09)'),
    ('reset_user_password',        'Reset a user password (AWA-10)'),
    ('bind_role',                  'Create a role binding (AWA-13)'),
    ('grant_permission',           'Create/revoke a permission grant (AWA-13)'),
    ('create_department',          'Create a department (AWA-14)'),
    -- Browser / behaviour
    ('view_browsing_history',      'View a student browsing summary (AIS-01)'),
    ('approve_whitelist',          'Approve a whitelist site request (SDA-04)'),
    ('view_suspicious_flags',      'Read suspicious-behaviour flags (AIS-07)'),
    -- Fees
    ('generate_fee_link',          'Generate a payment link (AWA-04)')
ON CONFLICT (code) DO NOTHING;

-- Default permission bundles per role.
-- Lecturer: day-to-day teaching concerns.
-- HOD: lecturer permissions + department-level admin.
-- Finance: marks + fees.
-- IT: tenancy + permissions + browsing visibility.
-- Admin: full access.
INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'lecturer', code FROM permissions
WHERE code IN (
    'mark_attendance', 'view_attendance_alerts',
    'create_assignment', 'grade_submission',
    'view_plagiarism_report', 'view_copy_check', 'view_ai_detection',
    'add_internal_marks',
    'create_group', 'create_event',
    'view_suspicious_flags'
)
ON CONFLICT DO NOTHING;

INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'hod', code FROM permissions
WHERE code IN (
    'mark_attendance', 'view_attendance_alerts',
    'create_assignment', 'grade_submission',
    'view_plagiarism_report', 'view_copy_check', 'view_ai_detection',
    'add_internal_marks', 'add_external_marks', 'approve_external_marks',
    'create_group', 'create_event',
    'view_suspicious_flags', 'view_browsing_history',
    'create_timetable', 'edit_timetable_slot', 'review_timetable_request',
    'create_user', 'reset_user_password',
    'bind_role', 'grant_permission', 'create_department'
)
ON CONFLICT DO NOTHING;

INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'finance', code FROM permissions
WHERE code IN (
    'add_external_marks', 'approve_external_marks',
    'generate_fee_link'
)
ON CONFLICT DO NOTHING;

INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'it', code FROM permissions
WHERE code IN (
    'create_user', 'reset_user_password',
    'bind_role', 'grant_permission', 'create_department',
    'approve_whitelist',
    'view_browsing_history', 'view_suspicious_flags'
)
ON CONFLICT DO NOTHING;

INSERT INTO role_default_permissions (role_code, permission_code)
SELECT 'admin', code FROM permissions
ON CONFLICT DO NOTHING;

COMMIT;
