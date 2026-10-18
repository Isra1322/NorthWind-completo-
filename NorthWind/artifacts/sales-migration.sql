BEGIN TRANSACTION;
GO

ALTER TABLE [Products] ADD [RowVersion] rowversion NOT NULL;
GO

ALTER TABLE [Products] ADD [UnitPrice] decimal(8,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [Orders] ADD [Subtotal] decimal(18,2) NULL;
GO

ALTER TABLE [Orders] ADD [TaxAmount] decimal(18,2) NULL;
GO

ALTER TABLE [Orders] ADD [TaxRate] decimal(7,6) NULL;
GO

ALTER TABLE [Orders] ADD [Total] decimal(18,2) NULL;
GO

ALTER TABLE [OrderDetails] ADD [ProductName] nvarchar(60) NULL;
GO

ALTER TABLE [Customers] ADD [Name] nvarchar(100) NOT NULL DEFAULT N'';
GO

UPDATE c SET Name = c.Id FROM Customers c WHERE c.Name = '';
UPDATE p SET UnitPrice = price.UnitPrice
FROM Products p
CROSS APPLY (
    SELECT TOP (1) d.UnitPrice FROM OrderDetails d
    JOIN Orders o ON o.Id = d.OrderId
    WHERE d.ProductId = p.Id AND d.UnitPrice > 0
    ORDER BY o.OrderDate DESC, o.Id DESC
) price
WHERE p.UnitPrice = 0;
UPDATE d SET ProductName = p.Name
FROM OrderDetails d JOIN Products p ON p.Id = d.ProductId
WHERE d.ProductName IS NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261011203903_CompleteSalesRequirements', N'8.0.22');
GO

COMMIT;
GO

