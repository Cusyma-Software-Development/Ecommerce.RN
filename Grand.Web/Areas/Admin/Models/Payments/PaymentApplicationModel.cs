using Grand.Core.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Areas.Admin.Models.Payments
{
    public class PaymentApplicationModel
    {
        public Order order { get; set; }
        public List<string> paymentTypes { get; set; }

        public string formattedBalance { get; set; }
        public string formattedOrderAmount { get; set; }
        public string formattedAmountPaid { get; set; }
    }
}
