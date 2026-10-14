-- Batch path: the whole batch arrives as one table-valued parameter and is upserted with one
-- set-based MERGE inside one transaction (ADR 0001).
CREATE OR ALTER PROCEDURE dbo.usp_UpsertItemsBatch
    @TenantId UNIQUEIDENTIFIER,
    @Rows     dbo.ItemBatchRow READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON; -- any error rolls back the whole batch

    DECLARE @now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @results TABLE (RowIndex INT NOT NULL PRIMARY KEY, Outcome VARCHAR(16) NOT NULL);

    -- MERGE fails if two source rows hit the same target row, so the first occurrence of a SKU
    -- wins and later ones are reported as duplicates instead of failing the batch.
    SELECT RowIndex, Sku, Name, Price, Currency, Quantity,
           ROW_NUMBER() OVER (PARTITION BY Sku ORDER BY RowIndex) AS Occurrence
    INTO #ranked
    FROM @Rows;

    BEGIN TRANSACTION;

    INSERT INTO @results (RowIndex, Outcome)
    SELECT RowIndex, 'duplicate' FROM #ranked WHERE Occurrence > 1;

    MERGE dbo.Items WITH (HOLDLOCK) AS target
    USING (SELECT RowIndex, Sku, Name, Price, Currency, Quantity FROM #ranked WHERE Occurrence = 1) AS source
        ON target.TenantId = @TenantId AND target.Sku = source.Sku
    WHEN MATCHED THEN
        UPDATE SET Name = source.Name, Price = source.Price, Currency = source.Currency,
                   Quantity = source.Quantity, UpdatedAt = @now
    WHEN NOT MATCHED THEN
        INSERT (TenantId, Sku, Name, Price, Currency, Quantity, CreatedAt, UpdatedAt)
        VALUES (@TenantId, source.Sku, source.Name, source.Price, source.Currency, source.Quantity, @now, @now)
    OUTPUT source.RowIndex, CASE $action WHEN 'INSERT' THEN 'created' ELSE 'updated' END
        INTO @results (RowIndex, Outcome);

    COMMIT TRANSACTION;

    SELECT RowIndex, Outcome FROM @results ORDER BY RowIndex;
END;
