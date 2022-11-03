using Grand.Plugin.Payments.BankTransfer.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.BankTransfer.Components
{
    [ViewComponent(Name = "BankTransfer")]
    public class BankTransferViewComponent : ViewComponent
    {
        public BankTransferViewComponent()
        {

        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.BankTransfer/Views/BankTransfer/BankTransfer.cshtml", model);
        }
    }
}
