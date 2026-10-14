-- Read model (ADR 0012): one row per payment, flattened for the tenant's payment history.
-- Filled only by the payments.read-model consumer from payment.captured / payment.refunded events,
-- so it is eventually consistent with the write tables.
CREATE TABLE dbo.TenantPayments
(
    PaymentId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantPayments PRIMARY KEY,
    TenantId   UNIQUEIDENTIFIER NOT NULL,
    InvoiceId  UNIQUEIDENTIFIER NOT NULL,
    Kind       VARCHAR(16)      NOT NULL,
    Amount     DECIMAL(19, 4)   NOT NULL,
    Currency   CHAR(3)          NOT NULL,
    OccurredAt DATETIME2(3)     NOT NULL
);
CREATE INDEX IX_TenantPayments_Tenant_OccurredAt ON dbo.TenantPayments (TenantId, OccurredAt DESC) INCLUDE (InvoiceId, Kind, Amount, Currency);

-- Inbox: message ids already applied, so a redelivered event is a no-op.
CREATE TABLE dbo.Inbox
(
    MessageId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Inbox PRIMARY KEY,
    ProcessedAt DATETIME2(3)     NOT NULL
);
