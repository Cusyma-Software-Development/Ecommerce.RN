using Grand.Core;
using Grand.Core.Domain.Catalog;
using Grand.Framework.Components;
using Grand.Services.Catalog;
using Grand.Web.Features.Models.Products;
using Grand.Web.Models.Catalog;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Components
{
    public class HomePageCategoryItems : BaseViewComponent
    {
        #region Fields

        private readonly IProductService _productService;
        private readonly IStoreContext _storeContext;
        private readonly IMediator _mediator;
        private readonly CatalogSettings _catalogSettings;

        #endregion

        #region Constructors

        public HomePageCategoryItems(
            IProductService productService, 
            IStoreContext storeContext, 
            IMediator mediator, 
            CatalogSettings catalogSettings)
        {
            _productService = productService;
            _storeContext = storeContext;
            _mediator = mediator;
            _catalogSettings = catalogSettings;
        }

        #endregion

        #region Invoker

        public async Task<IViewComponentResult> InvokeAsync(int? productThumbPictureSize,string categoryId)
        {
            IList<string> categoryIds = new List<string>();
            categoryIds.Add(categoryId);
            var products = (await _productService.SearchProducts(
                categoryIds : categoryIds,
                storeId: _storeContext.CurrentStore.Id,
                visibleIndividuallyOnly: true,
                orderBy: ProductSortingEnum.Position,
                pageSize: _catalogSettings.NewProductsNumberOnHomePage)).products;

            if (!products.Any())
                return Content("");

            var model = await _mediator.Send(new GetProductOverview() {
                PreparePictureModel = true,
                PreparePriceModel = true,
                PrepareSpecificationAttributes = _catalogSettings.ShowSpecAttributeOnCatalogPages,
                ProductThumbPictureSize = productThumbPictureSize,
                Products = products,
            });

            return View(model);
        }

        #endregion

    }
}
