-- The catalog is reference data under review like code (ADR 0007). Changing what a plan grants = a new script.
INSERT INTO dbo.Plans (Id, Code) VALUES (1, N'Free'), (2, N'Pro'), (3, N'Marketplace');

INSERT INTO dbo.Permissions (Id, Code) VALUES
    (1, N'items:read'), (2, N'items:write'), (3, N'ingest:batch'), (4, N'exports:read'), (5, N'plan:manage');

INSERT INTO dbo.Roles (Id, Code, MaxBatchSize) VALUES
    (1, N'viewer', NULL),
    (2, N'writer', NULL),
    (3, N'batch-writer', 5000),
    (4, N'marketplace-seller', 1000),
    (5, N'tenant-admin', NULL);

INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES
    (1, 1),
    (2, 1), (2, 2),
    (3, 1), (3, 2), (3, 3),
    (4, 1), (4, 2), (4, 3), (4, 4),
    (5, 5);

INSERT INTO dbo.PlanRoles (PlanId, RoleId) VALUES
    (1, 1), (1, 2),
    (2, 1), (2, 2), (2, 3),
    (3, 1), (3, 2), (3, 4);
