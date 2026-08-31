    CREATE DATABASE InventoryDB;
GO
USE InventoryDB;
GO

-- ── 2. TABLES ──────────────────────────────────────────────

-- Vendors
CREATE TABLE Vendors (
    VendorId             INT          IDENTITY(1,1) PRIMARY KEY,
    Name                 NVARCHAR(150) NOT NULL,
    ContactEmail         NVARCHAR(200) NULL,
    ContactPhone         NVARCHAR(30)  NULL,
    PromisedLeadTimeDays INT           NOT NULL DEFAULT 7,
    ActualLeadTimeDays   FLOAT         NOT NULL DEFAULT 7,
    CreatedAt            DATETIME      NOT NULL DEFAULT GETDATE()
);

-- Products (base table for all types)
CREATE TABLE Products (
    ProductId            INT           IDENTITY(1,1) PRIMARY KEY,
    SKU                  NVARCHAR(50)  NOT NULL UNIQUE,
    Name                 NVARCHAR(200) NOT NULL,
    Category             NVARCHAR(100) NULL,
    VendorId             INT           NOT NULL REFERENCES Vendors(VendorId),
    CurrentStock         INT           NOT NULL DEFAULT 0,
    AverageDailyDemand   FLOAT         NOT NULL DEFAULT 0,
    UnitCost             DECIMAL(10,2) NOT NULL DEFAULT 0,
    OrderingCost         DECIMAL(10,2) NOT NULL DEFAULT 0,
    HoldingCost          DECIMAL(10,2) NOT NULL DEFAULT 0,
    LeadTimeDays         INT           NOT NULL DEFAULT 7,
    ProductType          NVARCHAR(20)  NOT NULL DEFAULT 'NonPerishable'
        CHECK (ProductType IN ('Perishable','NonPerishable')),
    CreatedAt            DATETIME      NOT NULL DEFAULT GETDATE(),
    UpdatedAt            DATETIME      NOT NULL DEFAULT GETDATE()
);

-- Perishable-specific extension
CREATE TABLE PerishableDetails (
    ProductId     INT      NOT NULL PRIMARY KEY REFERENCES Products(ProductId) ON DELETE CASCADE,
    ExpiryDate    DATE     NOT NULL,
    ShelfLifeDays INT      NOT NULL DEFAULT 365
);

-- Non-perishable extension
CREATE TABLE NonPerishableDetails (
    ProductId              INT   NOT NULL PRIMARY KEY REFERENCES Products(ProductId) ON DELETE CASCADE,
    WeightKg               FLOAT NOT NULL DEFAULT 0,
    VolumeM3               FLOAT NOT NULL DEFAULT 0,
    RequiresClimateControl BIT   NOT NULL DEFAULT 0
);

-- Sales history
CREATE TABLE SalesHistory (
    SalesId      INT           IDENTITY(1,1) PRIMARY KEY,
    ProductId    INT           NOT NULL REFERENCES Products(ProductId),
    SaleDate     DATE          NOT NULL,
    QuantitySold INT           NOT NULL DEFAULT 0,
    Revenue      DECIMAL(12,2) NOT NULL DEFAULT 0,
    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
);
CREATE INDEX IX_SalesHistory_ProductId_Date ON SalesHistory(ProductId, SaleDate);


-- ── 3. VIEWS ───────────────────────────────────────────────

-- vw_AllProducts 
CREATE OR ALTER VIEW vw_AllProducts AS
SELECT
    p.ProductId,
    p.SKU,
    p.Name,
    p.Category,
    p.VendorId,
    p.CurrentStock,
    p.AverageDailyDemand,
    p.UnitCost,
    p.OrderingCost,
    p.HoldingCost,
    p.LeadTimeDays,
    p.ProductType,
    -- Perishable columns (NULL for non-perishable)
    pd.ExpiryDate,
    pd.ShelfLifeDays,
    -- Non-perishable columns (NULL for perishable)
    npd.WeightKg,
    npd.VolumeM3,
    npd.RequiresClimateControl
FROM Products p
LEFT JOIN PerishableDetails  pd  ON p.ProductId = pd.ProductId
LEFT JOIN NonPerishableDetails npd ON p.ProductId = npd.ProductId;
GO


-- vw_StockFlowReport: highlights items below ROP

CREATE OR ALTER VIEW vw_StockFlowReport AS
SELECT
    p.ProductId,
    p.SKU,
    p.Name,
    p.Category,
    v.Name                              AS VendorName,
    p.CurrentStock,
    p.AverageDailyDemand,
    p.LeadTimeDays,
    50                                  AS SafetyStock,
    (p.AverageDailyDemand * p.LeadTimeDays + 50) AS ReorderPoint,
    CASE
        WHEN p.CurrentStock <= (p.AverageDailyDemand * p.LeadTimeDays + 50)  -- rop form
        THEN 'CRITICAL'
        ELSE 'OK'
    END                                 AS StockStatus
FROM Products p
INNER JOIN Vendors v ON p.VendorId = v.VendorId;
GO

-- vw_VendorPerformance: calculates and ranks vendors

CREATE OR ALTER VIEW vw_VendorPerformance AS
SELECT
    v.VendorId,
    v.Name,
    v.PromisedLeadTimeDays,
    v.ActualLeadTimeDays,
    v.ActualLeadTimeDays - v.PromisedLeadTimeDays  AS LeadTimeVariance,
    CASE
        WHEN v.ActualLeadTimeDays = 0 THEN 0
        ELSE ROUND(
            CAST(v.PromisedLeadTimeDays AS FLOAT) / v.ActualLeadTimeDays * 100,
            1)
    END                                            AS PerformanceScore,
    CASE
        WHEN (CAST(v.PromisedLeadTimeDays AS FLOAT) / NULLIF(v.ActualLeadTimeDays,0) * 100) >= 85
        THEN 'Good'
        ELSE 'Needs Improvement'
    END                                            AS Rating
FROM Vendors v;
GO

-- vw_MonthlySalesSummary: monthly aggregates per product
CREATE OR ALTER VIEW vw_MonthlySalesSummary AS
SELECT
    ProductId,
    YEAR(SaleDate)  AS SaleYear,
    MONTH(SaleDate) AS SaleMonth,
    SUM(QuantitySold) AS TotalQty,
    SUM(Revenue)      AS TotalRevenue
FROM SalesHistory
GROUP BY ProductId, YEAR(SaleDate), MONTH(SaleDate);
GO 


-- ── 4. STORED PROCEDURES ────────────────────

-- usp_UpsertProduct: INSERT or UPDATE a product
CREATE OR ALTER PROCEDURE usp_UpsertProduct
    @ProductId          INT,
    @SKU                NVARCHAR(50),
    @Name               NVARCHAR(200),
    @Category           NVARCHAR(100),
    @VendorId           INT,
    @CurrentStock       INT,
    @AverageDailyDemand FLOAT,
    @UnitCost           DECIMAL(10,2),
    @OrderingCost       DECIMAL(10,2),
    @HoldingCost        DECIMAL(10,2),
    @LeadTimeDays       INT,
    @ProductType        NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Products WHERE ProductId = @ProductId)
    BEGIN
        UPDATE Products
        SET SKU = @SKU, Name = @Name, Category = @Category,
            VendorId = @VendorId, CurrentStock = @CurrentStock,
            AverageDailyDemand = @AverageDailyDemand, UnitCost = @UnitCost,
            OrderingCost = @OrderingCost, HoldingCost = @HoldingCost,
            LeadTimeDays = @LeadTimeDays, ProductType = @ProductType,
            UpdatedAt = GETDATE()
        WHERE ProductId = @ProductId;
    END
    ELSE
    BEGIN
        INSERT INTO Products (SKU, Name, Category, VendorId, CurrentStock,
            AverageDailyDemand, UnitCost, OrderingCost, HoldingCost,
            LeadTimeDays, ProductType)
        VALUES (@SKU, @Name, @Category, @VendorId, @CurrentStock,
            @AverageDailyDemand, @UnitCost, @OrderingCost, @HoldingCost,
            @LeadTimeDays, @ProductType);
    END
END;
GO

-- usp_CalculateROP: SQL-side ROP for all products (batch)
CREATE OR ALTER PROCEDURE usp_CalculateROP
    @SafetyStock INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ProductId,
        Name,
        AverageDailyDemand,
        LeadTimeDays,
        @SafetyStock                                          AS SafetyStock,
        (AverageDailyDemand * LeadTimeDays + @SafetyStock)   AS ReorderPoint,
        CurrentStock,
        CASE WHEN CurrentStock <= (AverageDailyDemand * LeadTimeDays + @SafetyStock)
             THEN 1 ELSE 0 END                               AS IsBelowROP
    FROM Products
    ORDER BY IsBelowROP DESC, Name;
END;
GO

-- usp_GetMonthlySalesHistory: returns monthly totals oldest → newest
CREATE OR ALTER PROCEDURE usp_GetMonthlySalesHistory
    @ProductId INT,
    @Months    INT = 6
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        YEAR(SaleDate)    AS SaleYear,
        MONTH(SaleDate)   AS SaleMonth,
        SUM(QuantitySold) AS TotalQty
    FROM SalesHistory
    WHERE ProductId = @ProductId
      AND SaleDate  >= DATEADD(MONTH, -@Months, GETDATE())
    GROUP BY YEAR(SaleDate), MONTH(SaleDate)
    ORDER BY SaleYear, SaleMonth;
END;
GO

-- usp_WhatIfSimulation: projects sales at increased demand
CREATE OR ALTER PROCEDURE usp_WhatIfSimulation
    @DemandIncreasePct FLOAT = 30
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.ProductId,
        p.Name,
        p.AverageDailyDemand,
        p.AverageDailyDemand * (1 + @DemandIncreasePct / 100.0)         AS ProjectedDailyDemand,
        p.AverageDailyDemand * 30 * (1 + @DemandIncreasePct / 100.0)    AS ProjectedMonthlySales,
        SQRT(
            2 * (p.AverageDailyDemand * 365 * (1 + @DemandIncreasePct/100.0))
            * p.OrderingCost / NULLIF(p.HoldingCost, 0)
        )                                                                AS ProjectedEOQ
    FROM Products p;
END;
GO

-- usp_VendorPerformance: scored vendor ranking
CREATE OR ALTER PROCEDURE usp_VendorPerformance
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM vw_VendorPerformance
    ORDER BY PerformanceScore DESC;
END;
GO


--eoq func
CREATE OR ALTER FUNCTION dbo.ufn_TestEOQ
(
    @AnnualDemand INT,
    @OrderingCost DECIMAL(10,2),
    @HoldingCost DECIMAL(10,2)
)
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @EOQ DECIMAL(10,2);

    -- EOQ formula: SQRT((2 * D * S) / H)
    SET @EOQ = SQRT((2 * @AnnualDemand * @OrderingCost) / @HoldingCost);

    RETURN @EOQ;
END;
GO




-- runforecast and eoq 
CREATE OR ALTER FUNCTION dbo.ufn_RunForecast
(
    @ProductId INT,
    @AnnualDemand INT,
    @OrderingCost DECIMAL(10,2),
    @HoldingCost DECIMAL(10,2),
    @DailyDemand INT,
    @LeadTimeDays INT
)
RETURNS TABLE
AS
RETURN
(
    SELECT 
        @ProductId AS ProductId,
        CAST(SQRT((2 * @AnnualDemand * @OrderingCost) / @HoldingCost) AS DECIMAL(10,2)) AS EOQ,
        (@DailyDemand * @LeadTimeDays) AS ROP
);
GO

-- add product 

CREATE OR ALTER PROCEDURE usp_AddProduct
    @SKU                NVARCHAR(50),
    @Name               NVARCHAR(200),
    @Category           NVARCHAR(100),
    @VendorId           INT,
    @CurrentStock       INT,
    @AverageDailyDemand FLOAT,
    @UnitCost           DECIMAL(10,2),
    @OrderingCost       DECIMAL(10,2),
    @HoldingCost        DECIMAL(10,2),
    @LeadTimeDays       INT,
    @ProductType        NVARCHAR(20),
    -- Perishable only (pass NULL for NonPerishable)
    @ExpiryDate         DATE     = NULL,
    @ShelfLifeDays      INT      = NULL,
    -- NonPerishable only (pass NULL for Perishable)
    @WeightKg           FLOAT    = NULL,
    @VolumeM3           FLOAT    = NULL,
    @RequiresClimate    BIT      = 0,
    -- Output: new ProductId
    @NewProductId       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
 
    -- Validate SKU is unique
    IF EXISTS (SELECT 1 FROM Products WHERE SKU = @SKU)
    BEGIN
        RAISERROR('A product with SKU ''%s'' already exists.', 16, 1, @SKU);
        RETURN;
    END
 
    -- Validate VendorId exists
    IF NOT EXISTS (SELECT 1 FROM Vendors WHERE VendorId = @VendorId)
    BEGIN
        RAISERROR('VendorId %d does not exist.', 16, 1, @VendorId);
        RETURN;
    END
 
    
    INSERT INTO Products
        (SKU, Name, Category, VendorId, CurrentStock,
         AverageDailyDemand, UnitCost, OrderingCost,
         HoldingCost, LeadTimeDays, ProductType)
    VALUES
        (@SKU, @Name, @Category, @VendorId, @CurrentStock,
         @AverageDailyDemand, @UnitCost, @OrderingCost,
         @HoldingCost, @LeadTimeDays, @ProductType);
 
    SET @NewProductId = SCOPE_IDENTITY();
 
    -- Insert into correct extension table
    IF @ProductType = 'Perishable'
    BEGIN
        INSERT INTO PerishableDetails (ProductId, ExpiryDate, ShelfLifeDays)
        VALUES (
            @NewProductId,
            ISNULL(@ExpiryDate, DATEADD(DAY, 30, GETDATE())),
            ISNULL(@ShelfLifeDays, 30)
        );
    END
    ELSE
    BEGIN
        INSERT INTO NonPerishableDetails
            (ProductId, WeightKg, VolumeM3, RequiresClimateControl)
        VALUES (
            @NewProductId,
            ISNULL(@WeightKg, 0),
            ISNULL(@VolumeM3, 0),
            ISNULL(@RequiresClimate, 0)
        );
    END
END;
GO
 
 
-- ── 2. DELETE PRODUCT STORED PROCEDURE ───────────────────────
CREATE OR ALTER PROCEDURE usp_DeleteProduct
    @ProductId   INT,
    @RowsDeleted INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
 
    -- Check product exists
    IF NOT EXISTS (SELECT 1 FROM Products WHERE ProductId = @ProductId)
    BEGIN
        RAISERROR('Product with ID %d does not exist.', 16, 1, @ProductId);
        SET @RowsDeleted = 0;
        RETURN;
    END
 
    -- Delete child records first
    DELETE FROM SalesHistory         WHERE ProductId = @ProductId;
    DELETE FROM PerishableDetails    WHERE ProductId = @ProductId;
    DELETE FROM NonPerishableDetails WHERE ProductId = @ProductId;
 
    -- Delete base product
    DELETE FROM Products WHERE ProductId = @ProductId;
 
    SET @RowsDeleted = @@ROWCOUNT;
END;
GO




-- ── 5. SAMPLE DATA ─────────────────────────────────────────
INSERT INTO Vendors (Name, ContactEmail, ContactPhone, PromisedLeadTimeDays, ActualLeadTimeDays)
VALUES
    ('QuickSupply Co.',   'orders@quicksupply.com', '555-0101', 5,  5.5),
    ('Global Goods Ltd.', 'supply@globalgoods.com', '555-0202', 7,  9.0),
    ('FastTrack Inc.',    'info@fasttrack.com',     '555-0303', 3,  3.1);

INSERT INTO Products (SKU, Name, Category, VendorId, CurrentStock,
    AverageDailyDemand, UnitCost, OrderingCost, HoldingCost, LeadTimeDays, ProductType)
VALUES
    ('SKU-001', 'Whole Milk 1L',       'Dairy',       1, 200, 15.0, 1.50, 50.0, 0.30, 5,  'Perishable'),
    ('SKU-002', 'Stainless Bolt M10',  'Hardware',    2, 500, 5.0,  0.25, 30.0, 0.05, 7,  'NonPerishable'),
    ('SKU-003', 'Vitamin C 500mg',     'Pharmacy',    3, 120, 8.0,  2.00, 40.0, 0.40, 3,  'Perishable'),
    ('SKU-004', 'HDMI Cable 2m',       'Electronics', 2,  80, 3.0,  5.00, 25.0, 1.00, 7,  'NonPerishable'),
    ('SKU-005', 'Printed Circuit Brd', 'Electronics', 1,  25, 1.5, 45.00, 60.0, 9.00, 10, 'NonPerishable');

INSERT INTO PerishableDetails (ProductId, ExpiryDate, ShelfLifeDays) VALUES
    (1, DATEADD(DAY, 14, GETDATE()), 21),
    (3, DATEADD(DAY, 180, GETDATE()), 730);

    INSERT INTO SalesHistory (ProductId, SaleDate, QuantitySold, Revenue)
VALUES (1, '2026-01-15', 20, 30.00),
       (1, '2026-02-10', 15, 22.50),
       (1, '2026-03-05', 18, 27.00);


     