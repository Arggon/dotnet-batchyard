-- One-by-one path: one round trip per item. Kept on purpose as the baseline for the benchmark.
CREATE OR ALTER PROCEDURE dbo.usp_UpsertItem
    @TenantId UNIQUEIDENTIFIER,
    @Sku      NVARCHAR(64),
    @Name     NVARCHAR(200),
    @Price    DECIMAL(19, 4),
    @Currency CHAR(3),
    @Quantity INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @result TABLE (Outcome VARCHAR(16) NOT NULL);

    MERGE dbo.Items WITH (HOLDLOCK) AS target
    USING (SELECT @Sku AS Sku) AS source
        ON target.TenantId = @TenantId AND target.Sku = source.Sku
    WHEN MATCHED THEN
        UPDATE SET Name = @Name, Price = @Price, Currency = @Currency, Quantity = @Quantity, UpdatedAt = @now
    WHEN NOT MATCHED THEN
        INSERT (TenantId, Sku, Name, Price, Currency, Quantity, CreatedAt, UpdatedAt)
        VALUES (@TenantId, @Sku, @Name, @Price, @Currency, @Quantity, @now, @now)
    OUTPUT CASE $action WHEN 'INSERT' THEN 'created' ELSE 'updated' END INTO @result;

    SELECT Outcome FROM @result;
END;
