namespace Nop.Web.Models.Catalog;

public partial record PromotionsPageModel
{
    public bool HasActivePromotions { get; set; }

    public CatalogProductsModel ProductsModel { get; set; } = new();
}
