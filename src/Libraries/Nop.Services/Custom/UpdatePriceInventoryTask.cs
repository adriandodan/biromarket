using System.Globalization;
using System.Text;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Services.Custom;

/// <summary>
/// Scheduled task: descarcă feedul de la OfficeGalaxy, actualizează preț și stoc după SKU, apoi șterge fișierul
/// </summary>
public partial class UpdatePriceInventoryTask(
    ILogger logger,
    IHttpClientFactory httpClientFactory,
    INopFileProvider fileProvider,
    IProductService productService)
    : IScheduleTask
{
    #region Methods

    /// <summary>
    /// Descarcă CSV, citește COD PRODUS / STOC / PRET RRP, actualizează produsele și șterge fișierul
    /// </summary>
    public async Task ExecuteAsync()
    {
        const string feedUrl = "https://feed.officegalaxy.ro/v2?key=3e316dedb9c302e86692c19e42e3424f&model=price_stock&output_format=csv";

        // folder temporar pentru fișier
        var tempFolder = fileProvider.GetAbsolutePath("App_Data", "temp", "officegalaxy");
        fileProvider.CreateDirectory(tempFolder);
        var tempFilePath = fileProvider.Combine(tempFolder, $"officegalaxy_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");

        try
        {
            await logger.InformationAsync("UpdatePriceInventoryTask started: downloading feed from OfficeGalaxy.");

            // 1. Descarcă fișierul
            using (var client = httpClientFactory.CreateClient())
            using (var response = await client.GetAsync(feedUrl))
            {
                response.EnsureSuccessStatusCode();

                // Create the file and write content
                await using var fs = File.Create(tempFilePath);
                await response.Content.CopyToAsync(fs);
            }

            await logger.InformationAsync($"UpdatePriceInventoryTask: file downloaded to {tempFilePath}.");

            // 2. Citește și procesează CSV-ul
            await ProcessFeedAsync(tempFilePath);

            await logger.InformationAsync("UpdatePriceInventoryTask finished successfully.");
        }
        catch (Exception ex)
        {
            await logger.ErrorAsync("Error in UpdatePriceInventoryTask.", ex);
            // nu aruncăm mai departe dacă nu vrei să oprești taskul la eroare globală,
            // dar cum StopOnError este 0 în ScheduleTask, poți fie să arunci, fie nu.
            // throw;
        }
        finally
        {
            // 3. Șterge fișierul temporar
            try
            {
                if (fileProvider.FileExists(tempFilePath))
                {
                    fileProvider.DeleteFile(tempFilePath);
                    await logger.InformationAsync($"UpdatePriceInventoryTask: temporary file deleted: {tempFilePath}.");
                }
            }
            catch (Exception ex)
            {
                // logăm, dar nu mai aruncăm
                await logger.ErrorAsync($"Failed to delete temporary file {tempFilePath}.", ex);
            }
        }
    }

    private async Task ProcessFeedAsync(string filePath)
    {
        if (!fileProvider.FileExists(filePath))
        {
            await logger.WarningAsync($"UpdatePriceInventoryTask: file not found {filePath}.");
            return;
        }

        await using var stream = fileProvider.GetOrCreateFile(filePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);

        // 1. Header
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            await logger.WarningAsync("UpdatePriceInventoryTask: CSV header is empty.");
            return;
        }

        // Parse header using CSV parsing that handles quoted fields
        var headers = ParseCsvLine(headerLine);

        var indexCodProdus = GetColumnIndex(headers, "COD PRODUS");
        var indexStoc = GetColumnIndex(headers, "STOC");
        var indexPret = GetColumnIndex(headers, "PRET RRP");

        if (indexCodProdus == -1 || indexStoc == -1 || indexPret == -1)
        {
            await logger.ErrorAsync(
                $"UpdatePriceInventoryTask: required columns not found. COD PRODUS index = {indexCodProdus}, STOC index = {indexStoc}, PRET RRP index = {indexPret}");
            return;
        }

        var processed = 0;
        var updated = 0;
        var notFound = 0;
        var parseErrors = 0;

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse CSV line using proper CSV parsing that handles quoted fields
            var parts = ParseCsvLine(line);
            
            if (parts.Length <= Math.Max(indexPret, Math.Max(indexCodProdus, indexStoc)))
            {
                parseErrors++;
                continue;
            }

            var sku = parts[indexCodProdus].Trim();
            var stocRaw = parts[indexStoc].Trim();
            var pretRaw = parts[indexPret].Trim();

            if (string.IsNullOrEmpty(sku))
            {
                await logger.InformationAsync($"`SKU` is empty. {line}");
                parseErrors++;
                continue;
            }

            // parse stoc
            if (!int.TryParse(stocRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var stock))
            {
                await logger.InformationAsync($"Invalid stock quantity. {line}");
                parseErrors++;
                continue;
            }

            // parse preț
            if (!decimal.TryParse(pretRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                await logger.InformationAsync($"Invalid price. {line}");
                parseErrors++;
                continue;
            }

            processed++;

            // 2. Caut produsul după SKU în nopCommerce
            var product = await productService.GetProductBySkuAsync(sku);
            if (product == null)
            {
                notFound++;
                continue;
            }

            // 3. Capturează valorile vechi pentru logging
            var oldPrice = product.Price;
            var oldStock = product.StockQuantity;
            var priceChanged = oldPrice != price;
            var stockChanged = oldStock != stock;

            // 4. Actualizează stoc și preț doar dacă sunt diferite
            if (priceChanged || stockChanged)
            {
                product.StockQuantity = stock;
                product.Price = price;

                await productService.UpdateProductAsync(product);
                updated++;

                // 5. Log modificările cu valorile vechi și noi
                var changes = new List<string>();
                
                if (priceChanged)
                {
                    changes.Add($"Price: {oldPrice:F2} → {price:F2}");
                }
                
                if (stockChanged)
                {
                    changes.Add($"Stock: {oldStock} → {stock}");
                }

                await logger.InformationAsync($"UpdatePriceInventoryTask: Product '{sku}' updated - {string.Join(", ", changes)}");
            }
        }

        await logger.InformationAsync(
            $"UpdatePriceInventoryTask: processed={processed}, updated={updated}, notFound={notFound}, parseErrors={parseErrors}.");
    }

    /// <summary>
    /// Parses a CSV line that may contain quoted fields with commas
    /// </summary>
    /// <param name="line">The CSV line to parse</param>
    /// <returns>Array of field values</returns>
    private static string[] ParseCsvLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return Array.Empty<string>();

        var fields = new List<string>();
        var currentField = new StringBuilder();
        var insideQuotes = false;
        var i = 0;

        while (i < line.Length)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote (double quote)
                    currentField.Append('"');
                    i += 2;
                }
                else
                {
                    // Toggle quote state
                    insideQuotes = !insideQuotes;
                    i++;
                }
            }
            else if (ch == ',' && !insideQuotes)
            {
                // Field separator (outside of quotes)
                fields.Add(currentField.ToString());
                currentField.Clear();
                i++;
            }
            else
            {
                // Regular character
                currentField.Append(ch);
                i++;
            }
        }

        // Add the last field
        fields.Add(currentField.ToString());

        return fields.ToArray();
    }

    private static int GetColumnIndex(string[] headers, string columnName)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i].Trim(), columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    #endregion
}
