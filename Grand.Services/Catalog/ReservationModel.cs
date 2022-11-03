using Grand.Core.Domain.Orders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grand.Services.Catalog
{
    public class ReservationModel
    {
        public string id { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string classValue { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public string type { get; set; }
        public Guid orderId { get; set; }
    }
}
