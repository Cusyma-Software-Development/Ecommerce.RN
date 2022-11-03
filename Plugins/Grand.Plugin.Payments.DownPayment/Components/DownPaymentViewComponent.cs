using Grand.Plugin.Payments.DownPayment.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.DownPayment.Components
{
    [ViewComponent(Name = "DownPayment")]
    public class DownPaymentViewComponent : ViewComponent
    {
        public DownPaymentViewComponent()
        {

        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.DownPayment/Views/DownPayment/DownPayment.cshtml",model);
        }
    }
}
