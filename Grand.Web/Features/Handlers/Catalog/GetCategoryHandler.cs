using Grand.Core;
using Grand.Core.Caching;
using Grand.Core.Domain.Catalog;
using Grand.Core.Domain.Media;
using Grand.Services.Catalog;
using Grand.Services.Customers;
using Grand.Services.Directory;
using Grand.Services.Localization;
using Grand.Services.Media;
using Grand.Services.Seo;
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
    public class GetCategoryHandler : IRequestHandler<GetCategory, CategoryModel>
    {
        private readonly IMediator _mediator;
        private readonly IWebHelper _webHelper;
        private readonly IPriceFormatter _priceFormatter;
        private readonly ICurrencyService _currencyService;
        private readonly ICacheManager _cacheManager;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IPictureService _pictureService;
        private readonly ILocalizationService _localizationService;
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly CatalogSettings _catalogSettings;
        private readonly MediaSettings _mediaSettings;

        public GetCategoryHandler(
            IMediator mediator,
            IWebHelper webHelper,
            IPriceFormatter priceFormatter,
            ICurrencyService currencyService,
            ICacheManager cacheManager,
            IProductService productService,
            ICategoryService categoryService,
            IPictureService pictureService,
            ILocalizationService localizationService,
            ISpecificationAttributeService specificationAttributeService,
            CatalogSettings catalogSettings,
            MediaSettings mediaSettings)
        {
            _mediator = mediator;
            _webHelper = webHelper;
            _priceFormatter = priceFormatter;
            _currencyService = currencyService;
            _cacheManager = cacheManager;
            _productService = productService;
            _categoryService = categoryService;
            _pictureService = pictureService;
            _localizationService = localizationService;
            _specificationAttributeService = specificationAttributeService;
            _catalogSettings = catalogSettings;
            _mediaSettings = mediaSettings;
        }

        public async Task<CategoryModel> Handle(GetCategory request, CancellationToken cancellationToken)
        {
            var model = request.Category.ToModel(request.Language);
            var customer = request.Customer;
            var storeId = request.Store.Id;
            var languageId = request.Language.Id;
            var currency = request.Currency;

            if (request.Command != null && request.Command.OrderBy == null && request.Category.DefaultSort >= 0)
                request.Command.OrderBy = request.Category.DefaultSort;

            //view/sorting/page size
            var options = await _mediator.Send(new GetViewSortSizeOptions() {
                Command = request.Command,
                PagingFilteringModel = request.Command,
                Language = request.Language,
                AllowCustomersToSelectPageSize = request.Category.AllowCustomersToSelectPageSize,
                PageSizeOptions = request.Category.PageSizeOptions,
                PageSize = request.Category.PageSize
            });
            model.PagingFilteringContext = options.command;

            //price ranges
            model.PagingFilteringContext.PriceRangeFilter.LoadPriceRangeFilters(request.Category.PriceRanges, _webHelper, _priceFormatter);
            var selectedPriceRange = model.PagingFilteringContext.PriceRangeFilter.GetSelectedPriceRange(_webHelper, request.Category.PriceRanges);
            decimal? minPriceConverted = null;
            decimal? maxPriceConverted = null;
            if (selectedPriceRange != null)
            {
                if (selectedPriceRange.From.HasValue)
                    minPriceConverted = await _currencyService.ConvertToPrimaryStoreCurrency(selectedPriceRange.From.Value, currency);

                if (selectedPriceRange.To.HasValue)
                    maxPriceConverted = await _currencyService.ConvertToPrimaryStoreCurrency(selectedPriceRange.To.Value, currency);
            }


            //category breadcrumb
            if (_catalogSettings.CategoryBreadcrumbEnabled)
            {
                model.DisplayCategoryBreadcrumb = true;

                string breadcrumbCacheKey = string.Format(ModelCacheEventConst.CATEGORY_BREADCRUMB_KEY,
                    request.Category.Id,
                    string.Join(",", customer.GetCustomerRoleIds()),
                    storeId,
                    languageId);
                model.CategoryBreadcrumb = await _cacheManager.GetAsync(breadcrumbCacheKey, async () =>
                    (await _categoryService.GetCategoryBreadCrumb(request.Category))
                    .Select(catBr => new CategoryModel {
                        Id = catBr.Id,
                        Name = catBr.GetLocalized(x => x.Name, languageId),
                        SeName = catBr.GetSeName(languageId)
                    })
                    .ToList()
                );
            }

            //subcategories
            var subCategories = new List<CategoryModel.SubCategoryModel>();
            foreach (var x in (await _categoryService.GetAllCategoriesByParentCategoryId(request.Category.Id)).Where(x => !x.HideOnCatalog))
            {
                var subCatModel = new CategoryModel.SubCategoryModel {
                    Id = x.Id,
                    Name = x.GetLocalized(y => y.Name, languageId),
                    SeName = x.GetSeName(languageId),
                    Description = x.GetLocalized(y => y.Description, languageId),
                    Flag = x.Flag,
                    FlagStyle = x.FlagStyle
                };
                //prepare picture model
                subCatModel.PictureModel = new PictureModel {
                    Id = x.PictureId,
                    FullSizeImageUrl = await _pictureService.GetPictureUrl(x.PictureId),
                    ImageUrl = await _pictureService.GetPictureUrl(x.PictureId, _mediaSettings.CategoryThumbPictureSize),
                    Title = string.Format(_localizationService.GetResource("Media.Category.ImageLinkTitleFormat"), subCatModel.Name),
                    AlternateText = string.Format(_localizationService.GetResource("Media.Category.ImageAlternateTextFormat"), subCatModel.Name)
                };
                subCategories.Add(subCatModel);
            };

            model.SubCategories = subCategories;

            //featured products
            if (!_catalogSettings.IgnoreFeaturedProducts)
            {
                //We cache a value indicating whether we have featured products
                IPagedList<Product> featuredProducts = null;
                string cacheKey = string.Format(ModelCacheEventConst.CATEGORY_HAS_FEATURED_PRODUCTS_KEY, request.Category.Id,
                    string.Join(",", customer.GetCustomerRoleIds()), storeId);
                var hasFeaturedProductsCache = await _cacheManager.GetAsync<bool?>(cacheKey);
                if (!hasFeaturedProductsCache.HasValue)
                {
                    //no value in the cache yet
                    //let's load products and cache the result (true/false)
                    featuredProducts = (await _productService.SearchProducts(
                       pageSize: _catalogSettings.LimitOfFeaturedProducts,
                       categoryIds: new List<string> { request.Category.Id },
                       storeId: storeId,
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
                       categoryIds: new List<string> { request.Category.Id },
                       storeId: storeId,
                       visibleIndividuallyOnly: true,
                       featuredProducts: true)).products;
                }
                if (featuredProducts != null)
                {
                    model.FeaturedProducts = (await _mediator.Send(new GetProductOverview() {
                        Products = featuredProducts,
                    })).ToList();
                }
            }


            var categoryIds = new List<string>();
            categoryIds.Add(request.Category.Id);
            if (_catalogSettings.ShowProductsFromSubcategories)
            {
                //include subcategories
                categoryIds.AddRange(await _mediator.Send(new GetChildCategoryIds() { ParentCategoryId = request.Category.Id, Customer = request.Customer, Store = request.Store }));
            }
            //products
            IList<string> alreadyFilteredSpecOptionIds = model.PagingFilteringContext.SpecificationFilter.GetAlreadyFilteredSpecOptionIds(_webHelper);
            var products = (await _productService.SearchProducts(loadFilterableSpecificationAttributeOptionIds: !_catalogSettings.IgnoreFilterableSpecAttributeOption,
                categoryIds: categoryIds,
                storeId: storeId,
                visibleIndividuallyOnly: true,
                featuredProducts: _catalogSettings.IncludeFeaturedProductsInNormalLists ? null : (bool?)false,
                priceMin: minPriceConverted,
                priceMax: maxPriceConverted,
                filteredSpecs: alreadyFilteredSpecOptionIds,
                orderBy: (ProductSortingEnum)request.Command.OrderBy,
                pageIndex: request.Command.PageNumber - 1,
                pageSize: request.Command.PageSize));

            model.Products = (await _mediator.Send(new GetProductOverview() {
                PrepareSpecificationAttributes = _catalogSettings.ShowSpecAttributeOnCatalogPages,
                Products = products.products,
            })).ToList();

            model.PagingFilteringContext.LoadPagedList(products.products);

            //specs
            await model.PagingFilteringContext.SpecificationFilter.PrepareSpecsFilters(alreadyFilteredSpecOptionIds,
                products.filterableSpecificationAttributeOptionIds,
                _specificationAttributeService, _webHelper, _cacheManager, request.Language.Id);

            return model;
        }
    }
}
