using System.Collections.Generic;
using System;

namespace Grand.Web.Models.Checkout
{
    public class PlatformRedirectModel
    {
        public class GrandnodeOrderCreateObject
        {
            public string OrderId { get; set; }
            public string UserReferenceId { get; set; }
            public DateTime DateCreated { get; set; }
            public decimal NetTotal { get; set; }
            public decimal Subtotal { get; set; }
            public decimal Discounts { get; set; }
            public List<Item> Items { get; set; }

            public class Item
            {
                public string ItemName { get; set; }
                public decimal Quantity { get; set; }
                public decimal Price { get; set; }
                public decimal Subtotal { get; set; }
                public string ItemId { get; set; }
            }
        }
    }
}
