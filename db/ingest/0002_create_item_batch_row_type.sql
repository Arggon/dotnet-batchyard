-- Table-valued parameter for the batch path. RowIndex is the position in the client's request,
-- so results can be returned per row and in input order.
CREATE TYPE dbo.ItemBatchRow AS TABLE
(
    RowIndex INT            NOT NULL PRIMARY KEY,
    Sku      NVARCHAR(64)   NOT NULL,
    Name     NVARCHAR(200)  NOT NULL,
    Price    DECIMAL(19, 4) NOT NULL,
    Currency CHAR(3)        NOT NULL,
    Quantity INT            NOT NULL
);
