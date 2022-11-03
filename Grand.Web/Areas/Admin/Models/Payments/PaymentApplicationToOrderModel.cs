using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Areas.Admin.Models.Payments
{
    public class PaymentApplicationToOrderModel
    {
        public string orderId { get; set; }
        public string orderGuid { get; set; }
        public string amount { get; set; }
        public string paymentMethod { get; set; }
    }
}
