using Grand.Core;
using Grand.Core.Caching;
using Grand.Core.Domain.Catalog;
using Grand.Core.Domain.Media;
using Grand.Services.Catalog;
using Grand.Services.Customers;
using Grand.Services.Localization;
using Grand.Services.Media;
using Grand.Web.Extensions;
using Grand.Web.Features.Models.Catalog;
using Grand.Web.Features.Models.Products;
using Grand.Web.Infrastructure.Cache;
using Grand.Web.Models.Catalog;
using Grand.Web.Models.Media;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Grand.Web.Features.Handlers.Catalog
{
    public class GetCategoryFeaturedProductsHandler : IRequestHandler<GetCategoryFeaturedProducts, IList<CategoryModel>>
    {
        private readonly ICategoryService _categoryService;
        private readonly IProductService _productService;
        private readonly ICacheManager _cacheManager;
        private readonly IPictureService _pictureService;
        private readonly ILocalizationService _localizationService;
        private readonly IMediator _mediator;
        private readonly MediaSettings _mediaSettings;
        private readonly CatalogSettings _catalogSettings;

        public GetCategoryFeaturedProductsHandler(
            ICategoryService categoryService,
            IProductService productService,
            ICacheManager cacheManager,
            IPictureService pictureService,
            ILocalizationService localizationService,
            IMediator mediator,
            MediaSettings mediaSettings,
            CatalogSettings catalogSettings)
        {
            _categoryService = categoryService;
            _productService = productService;
            _cacheManager = cacheManager;
            _pictureService = pictureService;
            _localizationService = localizationService;
            _mediator = mediator;
            _mediaSettings = mediaSettings;
            _catalogSettings = catalogSettings;
        }

        public async Task<IList<CategoryModel>> Handle(GetCategoryFeaturedProducts request, CancellationToken cancellationToken)
        {
            string categoriesCacheKey = string.Format(ModelCacheEventConst.CATEGORY_FEATURED_PRODUCTS_HOMEPAGE_KEY,
                string.Join(",", request.Customer.GetCustomerRoleIds()), request.Store.Id,
                request.Language.Id);

            var model = await _cacheManager.GetAsync(categoriesCacheKey, async () =>
            {
                var catlistmodel = new List<CategoryModel>();
                foreach (var x in await _categoryService.GetAllCategoriesFeaturedProductsOnHomePage())
                {
                    var catModel = x.ToModel(request.Language);
                    //prepare picture model
                    catModel.PictureModel = new PictureModel {
                        Id = x.PictureId,
                        FullSizeImageUrl = await _pictureService.GetPictureUrl(x.PictureId),
                        ImageUrl = await _pictureService.GetPictureUrl(x.PictureId, _mediaSettings.CategoryThumbPictureSize),
                        Title = string.Format(_localizationService.GetResource("Media.Category.ImageLinkTitleFormat"), catModel.Name),
                        AlternateText = string.Format(_localizationService.GetResource("Media.Category.ImageAlternateTextFormat"), catModel.Name)
                    };
                    catlistmodel.Add(catModel);
                }
                return catlistmodel;
            });


            foreach (var item in model)
            {
                //We cache a value indicating whether we have featured products
                IPagedList<Product> featuredProducts = null;
                string cacheKey = string.Format(ModelCacheEventConst.CATEGORY_HAS_FEATURED_PRODUCTS_KEY, item.Id,
                    string.Join(",", request.Customer.GetCustomerRoleIds()), request.Store.Id);
                var hasFeaturedProductsCache = await _cacheManager.GetAsync<bool?>(cacheKey);
                if (!hasFeaturedProductsCache.HasValue)
                {
                    //no value in the cache yet
                    //let's load products and cache the result (true/false)
                    featuredProducts = (await _productService.SearchProducts(
                       pageSize: _catalogSettings.LimitOfFeaturedProducts,
                       categoryIds: new List<string> { item.Id },
                       storeId: request.Store.Id,
                       visibleIndividuallyOnly: true,
                       featuredProducts: true)).products;
                    hasFeaturedProductsCache = featuredProducts.Any();
                    await _cacheManager.SetAsync(cacheKey, hasFeaturedProductsCache, CommonHelper.CacheTimeMinutes);
                }
                if (hasFeaturedProductsCache.Value && featuredProducts == null)
                {
                    //cache indicates that the category has featured products
                    //let's load them
                    featuredProducts = (await _productService.SearchProducts(
                       pageSize: _catalogSettings.LimitOfFeaturedProducts,
                       categoryIds: new List<string> { item.Id },
                       storeId: request.Store.Id,
                       visibleIndividuallyOnly: true,
                       featuredProducts: true)).products;
                }
                if (featuredProducts != null)
                {
                    item.FeaturedProducts = (await _mediator.Send(new GetProductOverview() {
                        PrepareSpecificationAttributes = _catalogSettings.ShowSpecAttributeOnCatalogPages,
                        Products = featuredProducts,
                    })).ToList();
                }
            }

            return model;
        }
    }
}
