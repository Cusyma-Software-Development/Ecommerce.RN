using Grand.Plugin.Payments.Paynamics.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.Paynamics.Components
{
    [ViewComponent(Name = "Paynamics")]
    public class PaynamicsViewComponent : ViewComponent
    {
        public PaynamicsViewComponent()
        {

        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.Paynamics/Views/Paynamics/Paynamics.cshtml", model);
        }
    }
}
