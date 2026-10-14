-- M3: billing permissions, granted to tenant admins.
INSERT INTO dbo.Permissions (Id, Code) VALUES (6, N'payments:read'), (7, N'payments:write');
INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (5, 6), (5, 7);
