using Handmade.Domain.Catalog;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Tests;

public sealed class CatalogTests
{
    private static readonly Guid SellerId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateProduct_StartsDraft_AndRaisesEvent()
    {
        Product product = Product.Create(SellerId, CategoryId, "Handmade Bracelet", "handmade-bracelet", "A handmade leather bracelet.", 120m, "EGP", Now);

        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Contains(product.DomainEvents, e => e is Handmade.Domain.Catalog.Events.ProductCreated);
    }

    [Fact]
    public void NegativePrice_Throws()
    {
        DomainException ex = Assert.Throws<DomainException>(() =>
            Product.Create(SellerId, CategoryId, "Item", "item", "A handmade item description.", -1m, "EGP", Now));
        Assert.Equal(CatalogErrorCodes.InvalidPrice, ex.Code);
    }

    [Fact]
    public void Submit_WithoutImage_Throws()
    {
        Product product = Product.Create(SellerId, CategoryId, "Handmade Bracelet", "handmade-bracelet", "A handmade leather bracelet.", 120m, "EGP", Now);
        DomainException ex = Assert.Throws<DomainException>(() => product.Submit(Now));
        Assert.Equal(CatalogErrorCodes.ProductIncomplete, ex.Code);
    }

    [Fact]
    public void Lifecycle_DraftToPublishedToArchivedToDraft()
    {
        Product product = ReadyProduct();
        product.Submit(Now);
        Assert.Equal(ProductStatus.PendingReview, product.Status);

        product.Approve(Guid.CreateVersion7(), Now);
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.NotNull(product.PublishedAt);

        product.Archive(Now);
        Assert.Equal(ProductStatus.Archived, product.Status);

        product.Restore(Now);
        Assert.Equal(ProductStatus.Draft, product.Status);
    }

    [Fact]
    public void Reject_ThenResubmit()
    {
        Product product = ReadyProduct();
        product.Submit(Now);
        product.Reject(Guid.CreateVersion7(), "Images are too dark for the gallery.", Now);
        Assert.Equal(ProductStatus.Rejected, product.Status);

        product.Submit(Now);
        Assert.Equal(ProductStatus.PendingReview, product.Status);
        Assert.Null(product.RejectionReason);
    }

    [Fact]
    public void Archive_FromDraft_Throws()
    {
        Product product = ReadyProduct();
        ConflictException ex = Assert.Throws<ConflictException>(() => product.Archive(Now));
        Assert.Equal(CatalogErrorCodes.InvalidStateTransition, ex.Code);
    }

    [Fact]
    public void Published_RemainsEditable()
    {
        Product product = ReadyProduct();
        product.Submit(Now);
        product.Approve(Guid.CreateVersion7(), Now);
        product.UpdateDetails("Updated bracelet", "A handmade leather bracelet.", CategoryId, 140m, "EGP");
        Assert.Equal("Updated bracelet", product.Name);
        Assert.Equal(ProductStatus.Published, product.Status);
    }

    [Fact]
    public void OnlyOnePrimaryImage()
    {
        Product product = ReadyProduct();
        product.AddImage("a.jpg", "https://cdn.local/a.jpg", 1, true);
        product.AddImage("b.jpg", "https://cdn.local/b.jpg", 2, true);
        Assert.Single(product.Images, i => i.IsPrimary);
        Assert.Equal("b.jpg", product.Images.Single(i => i.IsPrimary).StorageKey);
    }

    [Fact]
    public void DuplicateVariantSku_OnSameProduct_Throws()
    {
        Product product = ReadyProduct();
        product.AddVariant("Small", "BRC-S", 100m, "EGP");
        ConflictException ex = Assert.Throws<ConflictException>(() => product.AddVariant("Medium", "BRC-S", 110m, "EGP"));
        Assert.Equal(CatalogErrorCodes.DuplicateSku, ex.Code);
    }

    [Fact]
    public void EnsureOwnedBy_OtherSeller_ThrowsNotFound()
    {
        Product product = ReadyProduct();
        Assert.Throws<NotFoundException>(() => product.EnsureOwnedBy(Guid.CreateVersion7()));
    }

    [Fact]
    public void Category_CannotBeOwnParent()
    {
        Category category = Category.Create("Jewelry", "jewelry", null, null, Now);
        DomainException ex = Assert.Throws<DomainException>(() => category.Update("Jewelry", "jewelry", null, category.Id));
        Assert.Equal(CatalogErrorCodes.InvalidParent, ex.Code);
    }

    [Fact]
    public void Slug_FromName_IsUrlSafe()
    {
        Assert.Equal("handmade-leather-bracelet", CatalogSlug.FromName("Handmade Leather Bracelet"));
    }

    [Fact]
    public void PendingReview_CannotEditUntilCancelled()
    {
        Product product = ReadyProduct();
        product.Submit(Now);
        ConflictException ex = Assert.Throws<ConflictException>(() =>
            product.UpdateDetails("New", "A handmade leather bracelet.", CategoryId, 130m, "EGP"));
        Assert.Equal(CatalogErrorCodes.ProductNotEditable, ex.Code);

        product.CancelSubmission();
        product.UpdateDetails("New name", "A handmade leather bracelet.", CategoryId, 130m, "EGP");
        Assert.Equal("New name", product.Name);
    }

    private static Product ReadyProduct()
    {
        Product product = Product.Create(
            SellerId,
            CategoryId,
            "Handmade Bracelet",
            "handmade-bracelet",
            "A handmade leather bracelet.",
            120m,
            "EGP",
            Now);
        product.AddImage("main.jpg", null, 1, true);
        return product;
    }
}
