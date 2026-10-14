-- Items are scoped per tenant; (TenantId, Sku) is the natural key used by every upsert.
CREATE TABLE dbo.Items
(
    TenantId  UNIQUEIDENTIFIER NOT NULL,
    Sku       NVARCHAR(64)     NOT NULL,
    Name      NVARCHAR(200)    NOT NULL,
    Price     DECIMAL(19, 4)   NOT NULL,
    Currency  CHAR(3)          NOT NULL,
    Quantity  INT              NOT NULL,
    CreatedAt DATETIME2(3)     NOT NULL,
    UpdatedAt DATETIME2(3)     NOT NULL,
    CONSTRAINT PK_Items PRIMARY KEY CLUSTERED (TenantId, Sku),
    CONSTRAINT CK_Items_Price CHECK (Price >= 0),
    CONSTRAINT CK_Items_Quantity CHECK (Quantity >= 0)
);
