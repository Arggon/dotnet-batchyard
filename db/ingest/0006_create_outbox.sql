-- Transactional outbox for ingest (ADR 0017): ingest.completed is written in the batch transaction.
CREATE TABLE dbo.Outbox
(
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Outbox PRIMARY KEY,
    Type        NVARCHAR(100)    NOT NULL,
    Payload     NVARCHAR(MAX)    NOT NULL,
    OccurredAt  DATETIME2(3)     NOT NULL,
    PublishedAt DATETIME2(3)     NULL,
    Attempts    INT              NOT NULL CONSTRAINT DF_Outbox_Attempts DEFAULT 0,
    TraceParent VARCHAR(55)      NULL
);
CREATE INDEX IX_Outbox_Unpublished ON dbo.Outbox (OccurredAt) WHERE PublishedAt IS NULL;
