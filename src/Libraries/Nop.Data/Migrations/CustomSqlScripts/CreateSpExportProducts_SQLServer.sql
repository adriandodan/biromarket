IF OBJECT_ID('custom_ExportAllProducts', 'P') IS NOT NULL
    DROP PROCEDURE custom_ExportAllProducts;
GO

CREATE PROCEDURE custom_ExportAllProducts
AS
BEGIN
    DECLARE @WebsiteUrl NVARCHAR(MAX) = 'https://shop.biromarket.ro/images/thumbs/';
    DECLARE @Limit INT = 100;

    WITH CategoryPaths AS (
        SELECT 
            c.Id,
            CAST(c.Name AS NVARCHAR(MAX)) AS FullPath
        FROM Category AS c
        WHERE c.ParentCategoryId = 0

        UNION ALL

        SELECT 
            c.Id,
            CAST(cp.FullPath + '>>' + c.Name AS NVARCHAR(MAX)) AS FullPath
        FROM Category AS c
        JOIN CategoryPaths AS cp ON c.ParentCategoryId = cp.Id
    ),
    SpecAttrCTE AS (
        SELECT
            m.ProductId,
            sa.Name AS AttributeName,
            ISNULL(m.CustomValue, sao.Name) AS AttributeValue,
            ROW_NUMBER() OVER (
                PARTITION BY m.ProductId
                ORDER BY m.DisplayOrder, m.Id
            ) AS rn
        FROM Product_SpecificationAttribute_Mapping m
        LEFT JOIN SpecificationAttributeOption sao ON sao.Id = m.SpecificationAttributeOptionId
        LEFT JOIN SpecificationAttribute sa ON sa.Id = sao.SpecificationAttributeId
    )

    SELECT TOP (@Limit)
        p.Sku AS SKU,
        p.Name AS ProductName,
        p.ShortDescription,
        p.FullDescription,
        p.Price,
        mfr.Name AS Brand,

        (
            SELECT STRING_AGG(pt.Name, ',')
            FROM Product_ProductTag_Mapping ppt
            JOIN ProductTag pt ON pt.Id = ppt.ProductTag_Id
            WHERE ppt.Product_Id = p.Id
        ) AS Etichete,

        (
            SELECT TOP 1 cp.FullPath
            FROM CategoryPaths cp
            JOIN Product_Category_Mapping pcm ON pcm.CategoryId = cp.Id
            WHERE pcm.ProductId = p.Id
            ORDER BY LEN(cp.FullPath) DESC
        ) AS Category,

        (
            SELECT STRING_AGG(
                @WebsiteUrl +
                RIGHT('0000000' + CAST(pic.Id AS VARCHAR), 7) + '_' +
                pic.SeoFilename + '_550.' +
                RIGHT(pic.MimeType, CHARINDEX('/', REVERSE(pic.MimeType)) - 1),
                ','
            )
            FROM Product_Picture_Mapping ppm
            JOIN Picture pic ON pic.Id = ppm.PictureId
            WHERE ppm.ProductId = p.Id
        ) AS Pictures,

        p.StockQuantity AS Stoc,

        a1.AttributeName AS AttributeName1,
        a1.AttributeValue AS AttributeValue1,
        a2.AttributeName AS AttributeName2,
        a2.AttributeValue AS AttributeValue2,
        a3.AttributeName AS AttributeName3,
        a3.AttributeValue AS AttributeValue3,
        a4.AttributeName AS AttributeName4,
        a4.AttributeValue AS AttributeValue4,
        a5.AttributeName AS AttributeName5,
        a5.AttributeValue AS AttributeValue5

    FROM Product p
    LEFT JOIN Product_Manufacturer_Mapping pmm ON pmm.ProductId = p.Id
    LEFT JOIN Manufacturer mfr ON mfr.Id = pmm.ManufacturerId
    LEFT JOIN SpecAttrCTE a1 ON a1.ProductId = p.Id AND a1.rn = 1
    LEFT JOIN SpecAttrCTE a2 ON a2.ProductId = p.Id AND a2.rn = 2
    LEFT JOIN SpecAttrCTE a3 ON a3.ProductId = p.Id AND a3.rn = 3
    LEFT JOIN SpecAttrCTE a4 ON a4.ProductId = p.Id AND a4.rn = 4
    LEFT JOIN SpecAttrCTE a5 ON a5.ProductId = p.Id AND a5.rn = 5

    ORDER BY p.Id;
END
GO
