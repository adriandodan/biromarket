using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Vendors;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.ExportImport;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Core.Domain.Catalog;
using Nop.Core.Http;
using Nop.Core.Infrastructure;
using ILogger = Nop.Services.Logging.ILogger;
using Nop.Core.Domain.Media;
using Nop.Data;
using Microsoft.AspNetCore.StaticFiles;
using ClosedXML.Excel;
using Nop.Services.Seo;

namespace Nop.Web.Areas.Admin.Controllers;

public class ProductCustomController : BaseAdminController
{
    private readonly IProductService _productService;
    private readonly IPictureService _pictureService;
    private readonly ICategoryService _categoryService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IWorkContext _workContext;
    private readonly IPermissionService _permissionService;
    private readonly IProductAttributeService _productAttributeService;
    protected readonly VendorSettings _vendorSettings;
    protected readonly CatalogSettings _catalogSettings;
    protected readonly INopFileProvider _fileProvider;
    protected readonly ILogger _logger;
    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly INopDataProvider _dataProvider;
    protected readonly IUrlRecordService _urlRecordService;
    private readonly IManufacturerService _manufacturerService;
    private readonly IProductTagService _productTagService;
    private readonly ISpecificationAttributeService _specificationAttributeService;


    public ProductCustomController(
        IProductService productService,
        IPictureService pictureService,
        ICategoryService categoryService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        IWorkContext workContext,
        VendorSettings vendorSettings,
        IPermissionService permissionService,
        IProductAttributeService productAttributeService,
        CatalogSettings catalogSettings, 
        INopFileProvider fileProvider, 
        ILogger logger, 
        IHttpClientFactory httpClientFactory, 
        INopDataProvider dataProvider, 
        IUrlRecordService urlRecordService, IManufacturerService manufacturerService, IProductTagService productTagService, ISpecificationAttributeService specificationAttributeService)
    {
        _productService = productService;
        _pictureService = pictureService;
        _pictureService = pictureService;
        _categoryService = categoryService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _workContext = workContext;
        _vendorSettings = vendorSettings;
        _permissionService = permissionService;
        _productAttributeService = productAttributeService;
        _catalogSettings = catalogSettings;
        _fileProvider = fileProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _dataProvider = dataProvider;
        _urlRecordService = urlRecordService;
        _manufacturerService = manufacturerService;
        _productTagService = productTagService;
        _specificationAttributeService = specificationAttributeService;
    }

    [HttpPost, ActionName("ImportExcelCustomVariants")]
    public virtual async Task<IActionResult> ImportExcelCustomVariantsAsync(IFormFile importExcelFile)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts))
            return AccessDeniedView();

        if (await _workContext.GetCurrentVendorAsync() != null && !_vendorSettings.AllowVendorsToImportProducts)
            // A vendor cannot import products
            return AccessDeniedView();

        try
        {
            if (importExcelFile != null && importExcelFile.Length > 0)
            {
                await ImportProductsFromXlsxCustomAsync(importExcelFile.OpenReadStream(), true);
            }
            else
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Common.UploadFile"));
                return RedirectToAction("List", "Product");
            }

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Imported"));
            return RedirectToAction("List", "Product");
        }
        catch (Exception exc)
        {
            await _notificationService.ErrorNotificationAsync(exc);
            return RedirectToAction("List", "Product");
        }
    }

    [HttpPost, ActionName("ImportExcelCustomSamples")]
    public virtual async Task<IActionResult> ImportExcelCustomSamplesAsync(IFormFile importExcelFile)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageProducts))
            return AccessDeniedView();

        if (await _workContext.GetCurrentVendorAsync() != null && !_vendorSettings.AllowVendorsToImportProducts)
            // A vendor cannot import products
            return AccessDeniedView();

        try
        {
            if (importExcelFile != null && importExcelFile.Length > 0)
            {
                await ImportProductsFromXlsxCustomAsync(importExcelFile.OpenReadStream(), false);
            }
            else
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Admin.Common.UploadFile"));
                return RedirectToAction("List", "Product");
            }

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Imported"));
            return RedirectToAction("List", "Product");
        }
        catch (Exception exc)
        {
            await _notificationService.ErrorNotificationAsync(exc);
            return RedirectToAction("List", "Product");
        }
    }

    public virtual async Task ImportProductsFromXlsxCustomAsync(Stream stream, bool isVariantsImport)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1); // Get the first worksheet
        var rowCount = worksheet.LastRowUsed().RowNumber(); // Get the last row used

        for (var row = 2; row <= rowCount; row++)
        {
            var column = 1;
            var sku = worksheet.Cell(row, column).GetString().Trim();
            column++;
            var productName = worksheet.Cell(row, column).GetString().Trim();
            column++;
            var shortDescription = worksheet.Cell(row, column).GetString().Trim();
            column++;
            var fullDescription = worksheet.Cell(row, column).GetString().Trim();
            column++;
            decimal.TryParse(worksheet.Cell(row, column).GetString().Trim(), out var price);
            column++;

            var brand = worksheet.Cell(row, column).GetString().Trim();
            column++;
            var productTags = worksheet.Cell(row, column).GetString().Trim();
            column++;
            var category = worksheet.Cell(row, column).GetString().Trim();
            column++;
            var pictures = worksheet.Cell(row, column).GetString().Trim();
            column++;
            int.TryParse(worksheet.Cell(row, column).GetString().Trim(), out var stockQuantity);
            column++;

            // Handle attributes and combinations
            var attributeName = worksheet.Cell(row, column).GetString().Trim(); // Assuming attribute name is in column 9
            column++;
            var attributeValue = worksheet.Cell(row, column).GetString().Trim(); // Assuming attribute value is in column 10
            column++;
            var variantSku = worksheet.Cell(row, column).GetString().Trim(); // Assuming variant SKU is in column 11
            column++;
            decimal.TryParse(worksheet.Cell(row, column).GetString().Trim(), out var variantPrice);
            column++;

            // Load or create the product as before
            var product = await _productService.GetProductBySkuAsync(sku);
            if (product == null)
            {
                product = new Product
                {
                    Sku = sku,
                    Name = productName,
                    ShortDescription = shortDescription,
                    FullDescription = fullDescription,
                    Price = price,
                    Published = true,
                    VisibleIndividually = true,
                    OrderMaximumQuantity = 10000,
                    OrderMinimumQuantity = 1,
                    ProductType = ProductType.SimpleProduct,
                    IsShipEnabled = true,
                    ManageInventoryMethod = ManageInventoryMethod.ManageStock,
                    ManageInventoryMethodId = 1,
                    DisplayStockAvailability = true,
                    StockQuantity = stockQuantity
                };
                await _productService.InsertProductAsync(product);
            }
            else
            {
                product.Name = productName;
                product.ShortDescription = shortDescription;
                product.FullDescription = fullDescription;
                product.Price = price;
                product.VisibleIndividually = true;
                product.OrderMaximumQuantity = 10000;
                product.OrderMinimumQuantity = 1;
                product.ProductType = ProductType.SimpleProduct;
                product.IsShipEnabled = true;
                product.ManageInventoryMethod = ManageInventoryMethod.ManageStock;
                product.ManageInventoryMethodId = 1;
                product.DisplayStockAvailability = true;
                product.StockQuantity = stockQuantity;

                await _productService.UpdateProductAsync(product);
            }

            //search engine name
            await _urlRecordService.SaveSlugAsync(product, await _urlRecordService.ValidateSeNameAsync(product, null, product.Name, true), 0);

            var productPictureId = 0;

            if (isVariantsImport)
            {
                // Handle pictures for the product
                var imagePathTemp = await DownloadFileAsync(pictures);
                productPictureId = await ImportProductImageUsingHashAsync(imagePathTemp, product.Sku);

                if (!string.IsNullOrEmpty(imagePathTemp))
                {
                    if (!_fileProvider.FileExists(imagePathTemp))
                        continue;

                    try
                    {
                        _fileProvider.DeleteFile(imagePathTemp);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            else
            {
                var picturesUrls = pictures.Split(",").Select(c => c.Trim()).ToList();
                foreach (var pictureUrl in picturesUrls)
                {// Handle pictures for the product
                    var imagePathTemp = await DownloadFileAsync(pictureUrl);

                    if (productPictureId == 0)
                    {
                        productPictureId = await ImportProductImageUsingHashAsync(imagePathTemp, product.Sku);
                    }

                    if (!string.IsNullOrEmpty(imagePathTemp))
                    {
                        if (!_fileProvider.FileExists(imagePathTemp))
                            continue;

                        try
                        {
                            _fileProvider.DeleteFile(imagePathTemp);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(brand?.Trim()))
            {
                // Handle brands
                var brandId = await GetBrandIdAsync(brand);
                await UpdateProductBrandAsync(product.Id, brandId);
            }

            if (!string.IsNullOrWhiteSpace(productTags?.Trim()))
            {
                // Handle product tags
                await HandleProductTagsAsync(productTags, product);
            }

            // Handle categories
            var categoryId = await GetCategoryIdAsync(category, productPictureId);
            await UpdateProductCategoriesAsync(product.Id, categoryId);

            if (isVariantsImport)
            {
                if (!string.IsNullOrWhiteSpace(attributeName))
                {
                    // Check for existing product attribute or create it
                    var productSpecification = await _specificationAttributeService.GetSpecificationAttributeByNameAsync(attributeName);
                    if (productSpecification == null)
                    {
                        productSpecification = new SpecificationAttribute() { Name = attributeName };
                        await _specificationAttributeService.InsertSpecificationAttributeAsync(productSpecification);
                    }

                    if (!string.IsNullOrWhiteSpace(attributeValue))
                    {
                        // Check for existing product attribute value or create it
                        var productSpecificationAttributeOption = await _specificationAttributeService.GetSpecificationAttributeOptionByNameAsync(attributeValue, productSpecification.Id);
                        if (productSpecificationAttributeOption == null)
                        {
                            productSpecificationAttributeOption = new SpecificationAttributeOption()
                            {
                                Name = attributeValue,
                                SpecificationAttributeId = productSpecification.Id
                            };
                            await _specificationAttributeService.InsertSpecificationAttributeOptionAsync(productSpecificationAttributeOption);
                        }

                        // Check for existing product attribute mapping or create it
                        var productSpecificationAttributeMapping = await _specificationAttributeService.GetProductSpecificationAttributeByProductIdAsync(product.Id, productSpecificationAttributeOption.Id);
                        if (productSpecificationAttributeMapping == null)
                        {
                            productSpecificationAttributeMapping = new ProductSpecificationAttribute()
                            {
                                ProductId = product.Id,
                                SpecificationAttributeOptionId = productSpecificationAttributeOption.Id,
                                ShowOnProductPage = true,
                                AllowFiltering = true
                            };

                            await _specificationAttributeService.InsertProductSpecificationAttributeAsync(productSpecificationAttributeMapping);
                        }
                    }

                    

                }

                if (!string.IsNullOrWhiteSpace(attributeName))
                {
                    // Check for existing product attribute or create it
                    var productAttribute = await _productAttributeService.GetProductAttributeByNameAsync(attributeName);
                    if (productAttribute == null)
                    {
                        productAttribute = new ProductAttribute { Name = attributeName };
                        await _productAttributeService.InsertProductAttributeAsync(productAttribute);
                    }

                    // Check for existing product attribute mapping or create it
                    var productAttributeMapping = await _productAttributeService.GetProductAttributeMappingsByProductIdAndAttributeIdAsync(product.Id, productAttribute.Id);
                    if (productAttributeMapping == null)
                    {
                        productAttributeMapping = new ProductAttributeMapping
                        {
                            ProductId = product.Id,
                            ProductAttributeId = productAttribute.Id,
                            IsRequired = true,
                            AttributeControlTypeId = (int)AttributeControlType.DropdownList,
                            TextPrompt = productAttribute.Name
                        };

                        await _productAttributeService.InsertProductAttributeMappingAsync(productAttributeMapping);
                    }

                    if (!string.IsNullOrWhiteSpace(attributeValue))
                    {
                        // Check for existing product attribute value or create it
                        var productAttributeValue = await _productAttributeService.GetProductAttributeValueByValueAndAttributeMappingIdAsync(attributeValue, productAttributeMapping.Id);
                        if (productAttributeValue == null)
                        {
                            productAttributeValue = new ProductAttributeValue { Name = attributeValue, ProductAttributeMappingId = productAttributeMapping.Id };
                            await _productAttributeService.InsertProductAttributeValueAsync(productAttributeValue);
                        }

                        // Create or update the attribute combination
                        var attributeCombination = await _productAttributeService.GetProductAttributeCombinationAsync(variantSku, product.Id, CreateAttributesXml(productAttributeMapping.Id, productAttributeValue.Id));
                        if (attributeCombination == null)
                        {
                            attributeCombination = new ProductAttributeCombination
                            {
                                Sku = variantSku,
                                ProductId = product.Id,
                                AttributesXml = CreateAttributesXml(productAttributeMapping.Id, productAttributeValue.Id),
                                OverriddenPrice = variantPrice,
                            };
                            await _productAttributeService.InsertProductAttributeCombinationAsync(attributeCombination);
                        }
                        else
                        {
                            attributeCombination.OverriddenPrice = variantPrice;
                            await _productAttributeService.UpdateProductAttributeCombinationAsync(attributeCombination);
                        }

                        // Handle attribute combination images
                        if (productPictureId > 0)
                        {
                            var attributeCombinationPictures = await _productAttributeService.GetProductAttributeCombinationPicturesAsync(attributeCombination.Id);
                            var attributeCombinationPicture = attributeCombinationPictures.FirstOrDefault();
                            var pictureId = productPictureId;
                            {
                                if (attributeCombinationPicture == null)
                                {
                                    var newAttributeCombinationPicture = new ProductAttributeCombinationPicture()
                                    {
                                        ProductAttributeCombinationId = attributeCombination.Id,
                                        PictureId = pictureId
                                    };
                                    await _productAttributeService.InsertProductAttributeCombinationPictureAsync(
                                        newAttributeCombinationPicture);
                                }
                                else
                                {
                                    attributeCombinationPicture.PictureId = pictureId;
                                }
                            }

                        }
                    }
                }
            }

        }

        await CleanupDatabaseAsync();
    }

    private async Task<int> GetBrandIdAsync(string brandName)
    {
        await CreateNewBrandAsync(brandName);
        var brand = await _manufacturerService.GetManufacturerByNameAsync(brandName);
        return brand.Id;
    }
    private async Task CreateNewBrandAsync(string brandName)
    {
        var existingBrand = await _manufacturerService.GetManufacturerByNameAsync(brandName);

        if (existingBrand != null)
        {
            return;
        }

        var newBrand = new Manufacturer()
        {
            Name = brandName,
            CreatedOnUtc = DateTime.UtcNow,
            //default values
            PageSize = _catalogSettings.DefaultCategoryPageSize,
            PageSizeOptions = _catalogSettings.DefaultCategoryPageSizeOptions,
            Published = true,
            AllowCustomersToSelectPageSize = true,
        };

        await _manufacturerService.InsertManufacturerAsync(newBrand);

        //search engine name
        var seName = await _urlRecordService.ValidateSeNameAsync(newBrand, null, newBrand.Name, true);
        await _urlRecordService.SaveSlugAsync(newBrand, seName, 0);

    }

    public async Task UpdateProductBrandAsync(int productId, int brandId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
            throw new ArgumentException("Product not found", nameof(productId));

        var existingBrandMappings = await _manufacturerService.GetProductManufacturersByProductIdAsync(productId);

        foreach (var existingMapping in existingBrandMappings)
        {
            if (existingMapping.ManufacturerId != brandId)
            {
                await _manufacturerService.DeleteProductManufacturerAsync(existingMapping);
            }
        }

        if (existingBrandMappings.All(mapping => mapping.ManufacturerId != brandId))
        {
            var newMapping = new ProductManufacturer()
            {
                ProductId = productId,
                ManufacturerId = brandId
            };
            await _manufacturerService.InsertProductManufacturerAsync(newMapping);
        }
    }

    private async Task HandleProductTagsAsync(string productTags, Product product)
    {
        // Parse the category path and return IDs
        var productTagNames = productTags.Split(",").Select(c => c.Trim()).ToList();

        foreach (var productTagName in productTagNames)
        {
            await CreateNewProductTagAsync(productTagName);
        }

        await _productTagService.UpdateProductTagsAsync(product, productTagNames.ToArray());
    }

    private async Task CreateNewProductTagAsync(string productTagName)
    {
        var existingProductTag = await _productTagService.GetProductTagByNameAsync(productTagName);
        if (existingProductTag != null)
        {
            return;
        }

        var newProductTag = new ProductTag()
        {
            Name = productTagName,
        };

        await _productTagService.InsertProductTagAsync(newProductTag);

        //search engine name
        var seName = await _urlRecordService.ValidateSeNameAsync(newProductTag, null, newProductTag.Name, true);
        await _urlRecordService.SaveSlugAsync(newProductTag, seName, 0);

    }

    private async Task<int> GetCategoryIdAsync(string categoryPath, int imageId)
    {
        // Parse the category path and return IDs
        var categoryNames = categoryPath.Split(">>").Select(c => c.Trim()).ToList();
        var productCategoryId = 0;
        
        for (var i = 0; i < categoryNames.Count; i++)
        {
            var categoryName = categoryNames[i];
            if (i == 0)
            {
                await CreateNewCategoryAsync(categoryName, 0, true, imageId);
            }
            else
            {
                var parentCategoryName = categoryNames[i - 1];
                var parentCategoryId = 0;
                var parentCategory = await _categoryService.GetCategoryByNameAsync(parentCategoryName);
                if (parentCategory != null)
                {
                    parentCategoryId = parentCategory.Id;
                }
                await CreateNewCategoryAsync(categoryName, parentCategoryId, false, imageId);
            }

            if (i == categoryNames.Count - 1)
            {
                var category = await _categoryService.GetCategoryByNameAsync(categoryName);
                productCategoryId = category.Id;
            }
        }

        return productCategoryId;
    }
    private async Task CreateNewCategoryAsync(string categoryName, int parentCategoryId, bool showOnHomePage, int imageId)
    {
        var existingCategory = await _categoryService.GetCategoryByNameAsync(categoryName);
        if (existingCategory != null)
        {
            return;
        }

        var newCategory = new Category()
        {
            Name = categoryName,
            ParentCategoryId = parentCategoryId,
            CreatedOnUtc = DateTime.UtcNow,
            //default values
            PageSize = _catalogSettings.DefaultCategoryPageSize,
            PageSizeOptions = _catalogSettings.DefaultCategoryPageSizeOptions,
            Published = true,
            IncludeInTopMenu = true,
            AllowCustomersToSelectPageSize = true,
            ShowOnHomepage = showOnHomePage,
            PictureId = imageId
        };

        await _categoryService.InsertCategoryAsync(newCategory);

        //search engine name
        var seName = await _urlRecordService.ValidateSeNameAsync(newCategory, null, newCategory.Name, true);
        await _urlRecordService.SaveSlugAsync(newCategory, seName, 0);

    }
    public async Task UpdateProductCategoriesAsync(int productId, int categoryId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
            throw new ArgumentException("Product not found", nameof(productId));

        // Get existing category mappings for the product
        var existingCategoryMappings = await _categoryService.GetProductCategoriesByProductIdAsync(productId);

        // Remove categories that are no longer associated
        foreach (var existingMapping in existingCategoryMappings)
        {
            if (existingMapping.CategoryId != categoryId)
            {
                await _categoryService.DeleteProductCategoryAsync(existingMapping);
            }
        }

        if (existingCategoryMappings.All(mapping => mapping.CategoryId != categoryId))
        {
            var newMapping = new ProductCategory
            {
                ProductId = productId,
                CategoryId = categoryId
            };
            await _categoryService.InsertProductCategoryAsync(newMapping);
        }
    }


    protected virtual async Task<string> DownloadFileAsync(string urlString)
    {
        if (string.IsNullOrEmpty(urlString))
            return string.Empty;

        if (!Uri.IsWellFormedUriString(urlString, UriKind.Absolute))
            return urlString;

        //ensure that temp directory is created
        var tempDirectory = _fileProvider.MapPath(ExportImportDefaults.UploadsTempPath);
        _fileProvider.CreateDirectory(tempDirectory);

        var fileName = _fileProvider.GetFileName(urlString);
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        var filePath = _fileProvider.Combine(tempDirectory, fileName);
        try
        {
            var client = _httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
            var fileData = await client.GetByteArrayAsync(urlString);
            await using var fs = new FileStream(filePath, FileMode.OpenOrCreate);
            fs.Write(fileData, 0, fileData.Length);

            return filePath;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("Download image failed", ex);
        }

        return string.Empty;
    }

    protected virtual async Task<int> ImportProductImageUsingHashAsync(string productPictureMetadata, string productSku)
    {
        // Fetch the product based on SKU
        var product = await _productService.GetProductBySkuAsync(productSku);
        if (product == null)
            return 0; // or handle the error if the product does not exist

        // Load existing product images IDs
        var productsImagesIds = await _productService.GetProductsImagesIdsAsync(new[] { product.Id });
        var allProductPictureIds = productsImagesIds.SelectMany(p => p.Value);

        // Load existing hashes
        var productPictureIds = allProductPictureIds as int[] ?? allProductPictureIds.ToArray();
        var allPicturesHashes = productPictureIds.Any()
            ? await _dataProvider.GetFieldHashesAsync<PictureBinary>(
                p => productPictureIds.Contains(p.PictureId),
                p => p.PictureId,
                p => p.BinaryData)
            : new Dictionary<int, string>();

        if (string.IsNullOrEmpty(productPictureMetadata))
            return 0;

        try
        {
            var mimeType = GetMimeTypeFromFilePath(productPictureMetadata);
            if (string.IsNullOrEmpty(mimeType))
                return 0;

            var newPictureBinary = await _fileProvider.ReadAllBytesAsync(productPictureMetadata);
            var seoFileName = await _pictureService.GetPictureSeNameAsync(product.Name);

            if (productPictureIds.Any())
            {
                var newImageHash = HashHelper.CreateHash(
                    newPictureBinary,
                    ExportImportDefaults.ImageHashAlgorithm,
                    _dataProvider.SupportedLengthOfBinaryHash - 1);

                // Check if the image already exists and get the PictureId if it does
                var existingPicture = allPicturesHashes
                    .FirstOrDefault(existingHash =>
                        existingHash.Value.Equals(newImageHash, StringComparison.OrdinalIgnoreCase));

                if (!existingPicture.Equals(default(KeyValuePair<int, string>)))
                    return existingPicture.Key; // Return the existing PictureId
            }

            var newPicture = await _pictureService.InsertPictureAsync(newPictureBinary, mimeType, seoFileName);

            await _productService.InsertProductPictureAsync(new ProductPicture
            {
                PictureId = newPicture.Id,
                DisplayOrder = 1,
                ProductId = product.Id
            });

            // Update the product to ensure it has the latest information
            await _productService.UpdateProductAsync(product);
            return newPicture.Id;
        }
        catch (Exception ex)
        {
            await LogPictureInsertErrorAsync(productPictureMetadata, ex);
            return 0;
        }
    }


    protected virtual string GetMimeTypeFromFilePath(string filePath)
    {
        new FileExtensionContentTypeProvider().TryGetContentType(filePath, out var mimeType);

        //set to jpeg in case mime type cannot be found
        return mimeType ?? _pictureService.GetPictureContentTypeByFileExtension(_fileProvider.GetFileExtension(filePath));
    }

    protected virtual async Task LogPictureInsertErrorAsync(string picturePath, Exception ex)
    {
        var extension = _fileProvider.GetFileExtension(picturePath);
        var name = _fileProvider.GetFileNameWithoutExtension(picturePath);

        var point = string.IsNullOrEmpty(extension) ? string.Empty : ".";
        var fileName = _fileProvider.FileExists(picturePath) ? $"{name}{point}{extension}" : string.Empty;

        await _logger.ErrorAsync($"Insert picture failed (file name: {fileName})", ex);
    }

    public string CreateAttributesXml(int productAttributeMappingId, int productAttributeValue)
    {
        return $"""<Attributes><ProductAttribute ID="{productAttributeMappingId}"><ProductAttributeValue><Value>{productAttributeValue}</Value></ProductAttributeValue></ProductAttribute></Attributes>""";
    }

    private async Task CleanupDatabaseAsync()
    {
        var specificationAttributes = await _specificationAttributeService.GetSpecificationAttributesAsync();

        foreach (var specificationAttribute in specificationAttributes)
        {
            if (string.IsNullOrWhiteSpace(specificationAttribute.Name))
            {
                await _specificationAttributeService.DeleteSpecificationAttributeAsync(specificationAttribute);
            }
        }

        var attributes = await _productAttributeService.GetAllProductAttributesAsync();
        foreach (var attribute in attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.Name))
            {
                await _productAttributeService.DeleteProductAttributeAsync(attribute);
            }
        }
    }

}

