-- Access: plans grant roles, roles carry permissions and restrictions, clients get roles through assignments.
CREATE TABLE dbo.Plans
(
    Id   INT          NOT NULL CONSTRAINT PK_Plans PRIMARY KEY,
    Code NVARCHAR(32) NOT NULL CONSTRAINT UQ_Plans_Code UNIQUE
);

CREATE TABLE dbo.Permissions
(
    Id   INT          NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
    Code NVARCHAR(64) NOT NULL CONSTRAINT UQ_Permissions_Code UNIQUE
);

CREATE TABLE dbo.Roles
(
    Id           INT          NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Code         NVARCHAR(64) NOT NULL CONSTRAINT UQ_Roles_Code UNIQUE,
    MaxBatchSize INT          NULL -- restriction carried by the role; NULL = no batch rights from this role
);

CREATE TABLE dbo.RolePermissions
(
    RoleId       INT NOT NULL CONSTRAINT FK_RolePermissions_Roles REFERENCES dbo.Roles (Id),
    PermissionId INT NOT NULL CONSTRAINT FK_RolePermissions_Permissions REFERENCES dbo.Permissions (Id),
    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE dbo.PlanRoles
(
    PlanId INT NOT NULL CONSTRAINT FK_PlanRoles_Plans REFERENCES dbo.Plans (Id),
    RoleId INT NOT NULL CONSTRAINT FK_PlanRoles_Roles REFERENCES dbo.Roles (Id),
    CONSTRAINT PK_PlanRoles PRIMARY KEY (PlanId, RoleId)
);

CREATE TABLE dbo.Tenants
(
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
    Name      NVARCHAR(200)    NOT NULL,
    PlanId    INT              NOT NULL CONSTRAINT FK_Tenants_Plans REFERENCES dbo.Plans (Id),
    CreatedAt DATETIME2(3)     NOT NULL
);

CREATE TABLE dbo.ApiClients
(
    Id         NVARCHAR(64)     NOT NULL CONSTRAINT PK_ApiClients PRIMARY KEY,
    TenantId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ApiClients_Tenants REFERENCES dbo.Tenants (Id),
    Name       NVARCHAR(200)    NOT NULL,
    SecretHash NVARCHAR(256)    NOT NULL, -- PBKDF2, never the secret itself
    CreatedAt  DATETIME2(3)     NOT NULL
);
CREATE INDEX IX_ApiClients_TenantId ON dbo.ApiClients (TenantId);

CREATE TABLE dbo.Assignments
(
    ClientId NVARCHAR(64) NOT NULL CONSTRAINT FK_Assignments_ApiClients REFERENCES dbo.ApiClients (Id),
    RoleId   INT          NOT NULL CONSTRAINT FK_Assignments_Roles REFERENCES dbo.Roles (Id),
    Source   VARCHAR(16)  NOT NULL CONSTRAINT CK_Assignments_Source CHECK (Source IN ('plan', 'manual')),
    CONSTRAINT PK_Assignments PRIMARY KEY (ClientId, RoleId)
);
