using Grand.Plugin.Payments.PlatformRedirect.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Grand.Plugin.Payments.PlatformRedirect.Components
{
    [ViewComponent(Name = "PlatformRedirect")]
    public class PlatformRedirectViewComponent : ViewComponent
    {
        public PlatformRedirectViewComponent()
        {

        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.PlatformRedirect/Views/PlatformRedirect/PlatformRedirect.cshtml", model);
        }
    }
}
