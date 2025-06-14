--DELIMITER $$

--DROP PROCEDURE IF EXISTS custom_ExportAllProducts$$

--CREATE PROCEDURE custom_ExportAllProducts(
--    IN withPictures BOOLEAN DEFAULT TRUE,
--    IN limitValue INT DEFAULT 100000
--)
--BEGIN
--    DECLARE WebsiteUrl TEXT DEFAULT 'https://shop.biromarket.ro/images/thumbs/';

--    WITH RECURSIVE CategoryPaths AS (
--        SELECT
--            c.Id,
--            c.Name AS FullPath
--        FROM Category AS c
--        WHERE c.ParentCategoryId = 0

--        UNION ALL

--        SELECT
--            c.Id,
--            CONCAT(cp.FullPath, '>>', c.Name) AS FullPath
--        FROM Category AS c
--        JOIN CategoryPaths AS cp ON c.ParentCategoryId = cp.Id
--    ),
--    SpecAttrCTE AS (
--        SELECT
--            m.ProductId,
--            sa.Name AS AttributeName,
--            COALESCE(m.CustomValue, sao.Name) AS AttributeValue,
--            ROW_NUMBER() OVER (
--                PARTITION BY m.ProductId
--                ORDER BY m.DisplayOrder, m.Id
--            ) AS rn
--        FROM Product_SpecificationAttribute_Mapping m
--        LEFT JOIN SpecificationAttributeOption sao ON sao.Id = m.SpecificationAttributeOptionId
--        LEFT JOIN SpecificationAttribute sa ON sa.Id = sao.SpecificationAttributeId
--    )
    
--    SELECT
--        p.Sku AS SKU,
--        p.Name AS ProductName,
--        p.ShortDescription,
--        p.FullDescription,
--        p.Price,
--        mfr.Name AS Brand,

--        (
--            SELECT GROUP_CONCAT(pt.Name SEPARATOR ',')
--            FROM Product_ProductTag_Mapping ppt
--            JOIN ProductTag pt ON pt.Id = ppt.ProductTag_Id
--            WHERE ppt.Product_Id = p.Id
--        ) AS Etichete,

--        (
--            SELECT cp.FullPath
--            FROM CategoryPaths cp
--            JOIN Product_Category_Mapping pcm ON pcm.CategoryId = cp.Id
--            WHERE pcm.ProductId = p.Id
--            ORDER BY CHAR_LENGTH(cp.FullPath) DESC
--            LIMIT 1
--        ) AS Category,

--        CASE 
--            WHEN withPictures THEN
--                (
--                    SELECT GROUP_CONCAT(
--                        CONCAT(
--                            WebsiteUrl,
--                            LPAD(pic.Id, 7, '0'), '_',
--                            pic.SeoFilename, '_550.',
--                            SUBSTRING_INDEX(pic.MimeType, '/', -1)
--                        )
--                        ORDER BY ppm.DisplayOrder
--                        SEPARATOR ','
--                    )
--                    FROM Product_Picture_Mapping ppm
--                    JOIN Picture pic ON pic.Id = ppm.PictureId
--                    WHERE ppm.ProductId = p.Id
--                )
--            ELSE ''
--        END AS Pictures,

--        p.StockQuantity AS Stoc,

--        a1.AttributeName AS AttributeName1,
--        a1.AttributeValue AS AttributeValue1,
--        a2.AttributeName AS AttributeName2,
--        a2.AttributeValue AS AttributeValue2,
--        a3.AttributeName AS AttributeName3,
--        a3.AttributeValue AS AttributeValue3,
--        a4.AttributeName AS AttributeName4,
--        a4.AttributeValue AS AttributeValue4,
--        a5.AttributeName AS AttributeName5,
--        a5.AttributeValue AS AttributeValue5

--    FROM Product p
--    LEFT JOIN Product_Manufacturer_Mapping pmm ON pmm.ProductId = p.Id
--    LEFT JOIN Manufacturer mfr ON mfr.Id = pmm.ManufacturerId
--    LEFT JOIN SpecAttrCTE a1 ON a1.ProductId = p.Id AND a1.rn = 1
--    LEFT JOIN SpecAttrCTE a2 ON a2.ProductId = p.Id AND a2.rn = 2
--    LEFT JOIN SpecAttrCTE a3 ON a3.ProductId = p.Id AND a3.rn = 3
--    LEFT JOIN SpecAttrCTE a4 ON a4.ProductId = p.Id AND a4.rn = 4
--    LEFT JOIN SpecAttrCTE a5 ON a5.ProductId = p.Id AND a5.rn = 5

--    ORDER BY p.Id
--    LIMIT limitValue;

--END$$

--DELIMITER ;
