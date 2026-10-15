-- Usage meter (ADR 0018): items ingested per tenant per UTC day, filled only by the access.usage consumer
-- from ingest.completed events. Eventually consistent with the ingest database.
CREATE TABLE dbo.TenantUsage
(
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    UsageDate    DATE             NOT NULL,
    Batches      INT              NOT NULL,
    ItemsCreated INT              NOT NULL,
    ItemsUpdated INT              NOT NULL,
    ItemsRejected INT             NOT NULL,
    CONSTRAINT PK_TenantUsage PRIMARY KEY (TenantId, UsageDate)
);

-- Inbox: message ids already applied, so a redelivered ingest.completed is counted once.
CREATE TABLE dbo.Inbox
(
    MessageId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Inbox PRIMARY KEY,
    ProcessedAt DATETIME2(3)     NOT NULL
);
