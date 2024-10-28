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
using System.Xml.Linq;
using ClosedXML.Excel;
using Nop.Services.Seo;
using System.Xml;

namespace Nop.Web.Areas.Admin.Controllers;

public partial class ProductCustomController : BaseAdminController
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


    public ProductCustomController(
        IProductService productService,
        IPictureService pictureService,
        ICategoryService categoryService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        IWorkContext workContext,
        VendorSettings vendorSettings,
        IPermissionService permissionService,
        IImportManager importManager, IProductAttributeService productAttributeService, CatalogSettings catalogSettings, INopFileProvider fileProvider, ILogger logger, IHttpClientFactory httpClientFactory, INopDataProvider dataProvider, IUrlRecordService urlRecordService)
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
    }

    [HttpPost]
    public virtual async Task<IActionResult> ImportExcelCustomAsync(IFormFile importExcelFile)
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
                await ImportProductsFromXlsxCustomAsync(importExcelFile.OpenReadStream());
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

    public virtual async Task ImportProductsFromXlsxCustomAsync(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1); // Get the first worksheet
        var rowCount = worksheet.LastRowUsed().RowNumber(); // Get the last row used

        for (var row = 2; row <= rowCount; row++)
        {
            var sku = worksheet.Cell(row, 1).GetString().Trim();
            var productName = worksheet.Cell(row, 2).GetString().Trim();
            var shortDescription = worksheet.Cell(row, 3).GetString().Trim();
            var fullDescription = worksheet.Cell(row, 4).GetString().Trim();
            decimal.TryParse(worksheet.Cell(row, 6).GetString().Trim(), out var price);

            var category = worksheet.Cell(row, 7).GetString().Trim();
            var pictures = worksheet.Cell(row, 8).GetString().Trim();

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
                    VisibleIndividually = true
                };
                await _productService.InsertProductAsync(product);
            }
            else
            {
                product.Name = productName;
                product.ShortDescription = shortDescription;
                product.FullDescription = fullDescription;
                product.Price = price;
                await _productService.UpdateProductAsync(product);
            }

            //search engine name
            await _urlRecordService.SaveSlugAsync(product, await _urlRecordService.ValidateSeNameAsync(product, null, product.Name, true), 0);
            // Handle categories
            var categoryId = await GetCategoryIdAsync(category);
            await UpdateProductCategoriesAsync(product.Id, categoryId);

            // Handle pictures for the product
            var productPictures = pictures.Split(',').Select(url => url.Trim()).ToArray();
            var imagePaths = new List<string>();
            foreach (var url in productPictures)
            {
                var imagePathTemp = await DownloadFileAsync(url);
                imagePaths.Add(imagePathTemp);
            }

            var productPictureIds = await ImportProductImagesUsingHashAsync(imagePaths, product.Sku);

            foreach (var imagePathTemp in imagePaths)
            {
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

            // Handle attributes and combinations
            var attributeName = worksheet.Cell(row, 9).GetString().Trim(); // Assuming attribute name is in column 9
            var attributeValue = worksheet.Cell(row, 10).GetString().Trim(); // Assuming attribute value is in column 10
            var variantSku = worksheet.Cell(row, 11).GetString().Trim(); // Assuming variant SKU is in column 11
            decimal.TryParse(worksheet.Cell(row, 12).GetString().Trim(), out var variantPrice);
            int.TryParse(worksheet.Cell(row, 13).GetString().Trim(), out var variantStockQuantity);

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
                    StockQuantity = variantStockQuantity
                };
                await _productAttributeService.InsertProductAttributeCombinationAsync(attributeCombination);
            }
            else
            {
                attributeCombination.OverriddenPrice = variantPrice;
                attributeCombination.StockQuantity = variantStockQuantity;
                await _productAttributeService.UpdateProductAttributeCombinationAsync(attributeCombination);
            }

            // Handle attribute combination images
            if (productPictureIds.Count > 0)
            {
                var attributeCombinationPictures = await _productAttributeService.GetProductAttributeCombinationPicturesAsync(attributeCombination.Id);
                if (attributeCombinationPictures.Count > 0)
                {
                    var attributeCombinationPicture = attributeCombinationPictures.FirstOrDefault();
                    var pictureId = productPictureIds.FirstOrDefault();
                    {
                        if (attributeCombinationPicture == null)
                        {
                            var newAttributeCombinationPicture = new ProductAttributeCombinationPicture()
                            {
                                ProductAttributeCombinationId = attributeCombination.Id, PictureId = pictureId
                            };
                            await _productAttributeService.InsertProductAttributeCombinationPictureAsync(
                                newAttributeCombinationPicture);
                        }
                    }
                }
            }
        }
    }

    private async Task<int> GetCategoryIdAsync(string categoryPath)
    {
        // Parse the category path and return IDs
        var categoryNames = categoryPath.Split(">>").Select(c => c.Trim()).ToList();
        var productCategoryId = 0;
        
        for (var i = 0; i < categoryNames.Count; i++)
        {
            var categoryName = categoryNames[i];
            if (i == 0)
            {
                await CreateNewCategoryAsync(categoryName, 0, true);
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
                await CreateNewCategoryAsync(categoryName, parentCategoryId, false);
            }

            if (i == categoryNames.Count - 1)
            {
                var category = await _categoryService.GetCategoryByNameAsync(categoryName);
                productCategoryId = category.Id;
            }
        }

        return productCategoryId;
    }
    private async Task CreateNewCategoryAsync(string categoryName, int parentCategoryId, bool showOnHomePage)
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
            IncludeInTopMenu = false,
            AllowCustomersToSelectPageSize = true,
            ShowOnHomepage = showOnHomePage,
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

    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task<List<int>> ImportProductImagesUsingHashAsync(IList<string> productPictureMetadata, string productSku)
    {
        // Fetch the product based on SKU
        var product = await _productService.GetProductBySkuAsync(productSku);
        if (product == null)
            return new List<int>(); // or handle the error if the product does not exist

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

        foreach (var picturePath in productPictureMetadata)
        {
            if (string.IsNullOrEmpty(picturePath))
                continue;

            try
            {
                var mimeType = GetMimeTypeFromFilePath(picturePath);
                if (string.IsNullOrEmpty(mimeType))
                    continue;

                var newPictureBinary = await _fileProvider.ReadAllBytesAsync(picturePath);
                var pictureAlreadyExists = false;
                var seoFileName = await _pictureService.GetPictureSeNameAsync(product.Name);

                if (productPictureIds.Any())
                {
                    var newImageHash = HashHelper.CreateHash(
                        newPictureBinary,
                        ExportImportDefaults.ImageHashAlgorithm,
                        _dataProvider.SupportedLengthOfBinaryHash - 1);

                    // Check if the image already exists
                    pictureAlreadyExists = allPicturesHashes.Any(existingHash =>
                        existingHash.Value.Equals(newImageHash, StringComparison.OrdinalIgnoreCase));
                }

                if (pictureAlreadyExists)
                    continue; // Skip if the picture already exists

                var newPicture = await _pictureService.InsertPictureAsync(newPictureBinary, mimeType, seoFileName);

                await _productService.InsertProductPictureAsync(new ProductPicture
                {
                    PictureId = newPicture.Id,
                    DisplayOrder = 1,
                    ProductId = product.Id
                });

                // Update the product to ensure it has the latest information
                await _productService.UpdateProductAsync(product);
                return productPictureIds.ToList();
            }
            catch (Exception ex)
            {
                await LogPictureInsertErrorAsync(picturePath, ex);
            }
        }

        return [];
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


}

