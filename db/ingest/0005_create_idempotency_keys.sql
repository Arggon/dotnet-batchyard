-- Stored responses for Idempotency-Key replays (ADR 0005).
CREATE TABLE dbo.IdempotencyKeys
(
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    IdempotencyKey NVARCHAR(128)  NOT NULL,
    RequestHash  BINARY(32)       NOT NULL,
    StatusCode   INT              NOT NULL,
    ResponseBody NVARCHAR(MAX)    NOT NULL,
    CreatedAt    DATETIME2(3)     NOT NULL CONSTRAINT DF_IdempotencyKeys_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_IdempotencyKeys PRIMARY KEY CLUSTERED (TenantId, IdempotencyKey)
);
