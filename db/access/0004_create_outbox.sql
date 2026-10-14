-- Transactional outbox for Access (ADR 0011, 0013): plan.changed is written in the same transaction as the plan change.
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
