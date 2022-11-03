using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grand.Core.Domain.Common;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Framework.Controllers;
using Grand.Services.Catalog;
using Grand.Services.Orders;
using Grand.Services.Payments;
using Grand.Web.Areas.Admin.Models.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.OData.Edm;
using Org.BouncyCastle.Ocsp;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Grand.Web.Areas.Admin.Controllers
{
    public class PaymentsController : BaseAdminController
    {
        private Grand.Services.Payments.IPaymentService _paymentService;
        private IOrderService _orderService;
        private readonly IPriceFormatter _priceFormatter;

        public PaymentsController(IPaymentService paymentService,IOrderService orderService,IPriceFormatter priceFormatter)
        {
            _paymentService = paymentService;
            _orderService = orderService;
            _priceFormatter = priceFormatter;
        }

        public IActionResult Index() => RedirectToAction("List");

        public async Task<IActionResult> List()
        {
            List<PaymentCollections> response =await _orderService.GetPaymentCollections(Convert.ToDateTime(DateTime.UtcNow.ToShortDateString() + " 00:00:00"), Convert.ToDateTime(DateTime.UtcNow.ToShortDateString() + " 23:59:59"));
            return View(response);
        }

        [HttpPost]
        public async Task<IActionResult> List(string dateStart,string dateEnd)
        {
            List<PaymentCollections> response = await _orderService.GetPaymentCollections(Convert.ToDateTime(dateStart + " 00:00:00"), Convert.ToDateTime(dateEnd + " 23:59:59"));
            return View(response);
        }
        [HttpGet]
        public async Task<IActionResult> ApplyPayment(string OrderId)
        {
            PaymentApplicationModel response = new PaymentApplicationModel();
            Order order =await _orderService.GetOrderByGuid(Guid.Parse(OrderId));
            List<string> paymentMethods = new List<string>();
            paymentMethods.Add("Cash");
            paymentMethods.Add("Credit Card");
            paymentMethods.Add("Check");
            paymentMethods.Add("Down Payment");

            response.formattedBalance = _priceFormatter.FormatPrice(order.Balance, true, false);
            response.formattedAmountPaid = _priceFormatter.FormatPrice(order.AmountPaid, true, false);
            response.formattedOrderAmount = _priceFormatter.FormatPrice(order.OrderTotal, true, false);
            response.order = order;
            response.paymentTypes = paymentMethods;

            
            return View(response);
        }
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                
        [HttpPost]
     public async Task<IActionResult> ApplyPayment(PaymentApplicationModel model, string orderId,string paymentAmount,string paymentMethod,string orderGuid,string id,string referenceNumber,string remarks,string attachment,string attachmentFile)
        {
            GenericResponse response =await _orderService.ApplyPaymentToOrder(Convert.ToInt32(orderId), Convert.ToDecimal(paymentAmount), Guid.Parse(orderGuid), paymentMethod,referenceNumber,remarks,attachment,attachmentFile);
            if (!response.isSuccess)
            {
                ModelState.AddModelError(Guid.NewGuid().ToString(), response.message);

                PaymentApplicationModel modelObj = new PaymentApplicationModel();
                Order order = await _orderService.GetOrderByGuid(Guid.Parse(orderGuid));
                List<string> paymentMethods = new List<string>();    
                paymentMethods.Add("Cash");
                paymentMethods.Add("Credit Card"); 
                paymentMethods.Add("Check");
                paymentMethods.Add("Down Payment");

                modelObj.formattedBalance = _priceFormatter.FormatPrice(order.Balance, true, false);
                modelObj.formattedAmountPaid = _priceFormatter.FormatPrice(order.AmountPaid, true, false);
                modelObj.formattedOrderAmount = _priceFormatter.FormatPrice(order.OrderTotal, true, false);
                modelObj.order = order;
                modelObj.paymentTypes = paymentMethods;


                return View(modelObj);
            }
            return RedirectToAction("Edit", "Order", new { id = id });
        }
    }
}
