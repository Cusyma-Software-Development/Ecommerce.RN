﻿using Grand.Core;
using Grand.Core.Domain.Catalog;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Directory;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Seo;
using Grand.Services.Catalog;
using Grand.Services.Customers;
using Grand.Services.Directory;
using Grand.Services.Localization;
using Grand.Services.Orders;
using Grand.Web.Areas.Admin.Features.Model.Common;
using Grand.Web.Areas.Admin.Models.Home;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Areas.Admin.Controllers
{
    public partial class HomeController : BaseAdminController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly GoogleAnalyticsSettings _googleAnalyticsSettings;
        private readonly IWorkContext _workContext;
        private readonly IOrderReportService _orderReportService;
        private readonly ICustomerService _customerService;
        private readonly IMediator _mediator;
        private readonly IProductReservationService _productReservationService;

        #endregion

        #region Ctor

        public HomeController(
            ILocalizationService localizationService,
            GoogleAnalyticsSettings googleAnalyticsSettings,
            IWorkContext workContext,
            IOrderReportService orderReportService,
            ICustomerService customerService,
            IMediator mediator,
            IProductReservationService productReservationService)
        {
            _localizationService = localizationService;
            _googleAnalyticsSettings = googleAnalyticsSettings;
            _workContext = workContext;
            _orderReportService = orderReportService;
            _customerService = customerService;
            _mediator = mediator;
            _productReservationService = productReservationService;
        }

        #endregion

        #region Utiliti

        private async Task<DashboardActivityModel> PrepareActivityModel()
        {
            var model = new DashboardActivityModel();
            string vendorId = "";
            if (_workContext.CurrentVendor != null)
                vendorId = _workContext.CurrentVendor.Id;

            var storeId = string.Empty;
            if (_workContext.CurrentCustomer.IsStaff())
                //storeId = _workContext.CurrentCustomer.StaffStoreId;
                storeId = _workContext.CurrentCustomer.StoreId;

            model.OrdersPending = (await _orderReportService.GetOrderAverageReportLine(storeId: storeId, os: OrderStatus.Pending)).CountOrders;
            model.AbandonedCarts = (await _customerService.GetAllCustomers(storeId: storeId, loadOnlyWithShoppingCart: true, pageSize: 1)).TotalCount;

            HttpContext.RequestServices.GetRequiredService<IProductService>().GetLowStockProducts(vendorId, storeId, out IList<Product> products, out IList<ProductAttributeCombination> combinations);

            model.LowStockProducts = products.Count + combinations.Count;

            model.ReturnRequests = await _mediator.Send(new GetReturnRequest() { RequestStatusId = 0, StoreId = storeId });
            model.TodayRegisteredCustomers = (await _customerService.GetAllCustomers(storeId: storeId, customerRoleIds: new string[] { (await _customerService.GetCustomerRoleBySystemName(SystemCustomerRoleNames.Registered)).Id }, createdFromUtc: DateTime.UtcNow.Date, pageSize: 1)).TotalCount;
            return model;

        }

        #endregion

        #region Methods

        public IActionResult Index()
        {
            var model = new DashboardModel {
                IsLoggedInAsVendor = _workContext.CurrentVendor != null && !_workContext.CurrentCustomer.IsStaff()
            };
            if (string.IsNullOrEmpty(_googleAnalyticsSettings.gaprivateKey) ||
                string.IsNullOrEmpty(_googleAnalyticsSettings.gaserviceAccountEmail) ||
                string.IsNullOrEmpty(_googleAnalyticsSettings.gaviewID))
                model.HideReportGA = true;

            return View(model);
        }

        public IActionResult Statistics()
        {
            var model = new DashboardModel {
                IsLoggedInAsVendor = _workContext.CurrentVendor != null && !_workContext.CurrentCustomer.IsStaff()
            };
            return View(model);
        }

        public async Task<IActionResult> DashboardActivity()
        {
            var model = await PrepareActivityModel();
            return PartialView(model);
        }

        public async Task<IActionResult> GetReservationData(string StartDate,string EndDate)
        {
            var storeId = string.Empty;
            if (_workContext.CurrentCustomer.IsStaff())
                storeId = _workContext.CurrentCustomer.StoreId;

            var staffchecker = _workContext.CurrentCustomer.IsStaff();

            List<ReservationModel> response = await _productReservationService.GetProductReservationsByDatePeriod(Convert.ToDateTime(StartDate), Convert.ToDateTime(EndDate), storeId);
            int number = 1;
            response.ForEach(fe => {
                number += 1;
                fe.start = Convert.ToDateTime(fe.start).ToString("yyyy-MM-dd hh:mm tt");
                fe.end = Convert.ToDateTime(fe.end).AddHours(1).ToString("yyyy-MM-dd hh:mm tt");
            }); 
            return StatusCode(200,response);
        }

        public static long ConvertDatetimeToUnixTimeStamp(DateTime date)
        {
            var dateTimeOffset = new DateTimeOffset(date);
            var unixDateTime = dateTimeOffset.ToUnixTimeSeconds();
            return unixDateTime;
        }

        public async Task<IActionResult> SetLanguage(string langid, [FromServices] ILanguageService languageService, string returnUrl = "")
        {
            var language = await languageService.GetLanguageById(langid);
            if (language != null)
            {
                await _workContext.SetWorkingLanguage(language);
            }

            //home page
            if (String.IsNullOrEmpty(returnUrl))
                returnUrl = Url.Action("Index", "Home", new { area = "Admin" });
            //prevent open redirection attack
            if (!Url.IsLocalUrl(returnUrl))
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            return Redirect(returnUrl);
        }

        [AcceptVerbs("Get")]
        public async Task<IActionResult> GetStatesByCountryId([FromServices] ICountryService countryService, [FromServices] IStateProvinceService stateProvinceService,
            string countryId, bool? addSelectStateItem, bool? addAsterisk)
        {
            // This action method gets called via an ajax request
            if (String.IsNullOrEmpty(countryId))
                return Json(new List<dynamic>() { new { id = "", name = _localizationService.GetResource("Address.SelectState") } });

            var country = await countryService.GetCountryById(countryId);
            var states = country != null ? await stateProvinceService.GetStateProvincesByCountryId(country.Id, showHidden: true) : new List<StateProvince>();
            var result = (from s in states
                          select new { id = s.Id, name = s.Name }).ToList();
            if (addAsterisk.HasValue && addAsterisk.Value)
            {
                //asterisk
                result.Insert(0, new { id = "", name = "*" });
            }
            else
            {
                if (country == null)
                {
                    //country is not selected ("choose country" item)
                    if (addSelectStateItem.HasValue && addSelectStateItem.Value)
                    {
                        result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.SelectState") });
                    }
                    else
                    {
                        result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.OtherNonUS") });
                    }
                }
                else
                {
                    //some country is selected
                    if (result.Count == 0)
                    {
                        //country does not have states
                        result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.OtherNonUS") });
                    }
                    else
                    {
                        //country has some states
                        if (addSelectStateItem.HasValue && addSelectStateItem.Value)
                        {
                            result.Insert(0, new { id = "", name = _localizationService.GetResource("Admin.Address.SelectState") });
                        }
                    }
                }
            }
            return Json(result);
        }

        #endregion
    }
}
