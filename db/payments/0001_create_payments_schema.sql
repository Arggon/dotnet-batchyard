CREATE TABLE dbo.Invoices
(
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    TenantId  UNIQUEIDENTIFIER NOT NULL,
    Reference NVARCHAR(64)     NOT NULL,
    Amount    DECIMAL(19, 4)   NOT NULL CONSTRAINT CK_Invoices_Amount CHECK (Amount > 0),
    Currency  CHAR(3)          NOT NULL,
    Status    VARCHAR(16)      NOT NULL CONSTRAINT CK_Invoices_Status CHECK (Status IN ('open', 'paid', 'refunded')),
    CreatedAt DATETIME2(3)     NOT NULL,
    Version   ROWVERSION       NOT NULL -- optimistic concurrency token
);
CREATE INDEX IX_Invoices_TenantId ON dbo.Invoices (TenantId);

CREATE TABLE dbo.Payments
(
    Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    TenantId         UNIQUEIDENTIFIER NOT NULL,
    InvoiceId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Payments_Invoices REFERENCES dbo.Invoices (Id),
    Kind             VARCHAR(16)      NOT NULL CONSTRAINT CK_Payments_Kind CHECK (Kind IN ('capture', 'refund')),
    Amount           DECIMAL(19, 4)   NOT NULL,
    Currency         CHAR(3)          NOT NULL,
    IdempotencyKey   NVARCHAR(128)    NOT NULL,
    GatewayReference NVARCHAR(64)     NOT NULL,
    CreatedAt        DATETIME2(3)     NOT NULL
);
-- The rule "one charge per key" lives in the database, not only in code.
CREATE UNIQUE INDEX UX_Payments_Tenant_IdempotencyKey ON dbo.Payments (TenantId, IdempotencyKey);

-- Transactional outbox (ADR 0011): written in the same transaction as the change, published later.
CREATE TABLE dbo.Outbox
(
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Outbox PRIMARY KEY,
    Type        NVARCHAR(100)    NOT NULL,
    Payload     NVARCHAR(MAX)    NOT NULL,
    OccurredAt  DATETIME2(3)     NOT NULL,
    PublishedAt DATETIME2(3)     NULL,
    Attempts    INT              NOT NULL CONSTRAINT DF_Outbox_Attempts DEFAULT 0
);
CREATE INDEX IX_Outbox_Unpublished ON dbo.Outbox (OccurredAt) WHERE PublishedAt IS NULL;
