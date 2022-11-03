using Grand.Web.Features.Models.News;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Models.Checkout
{
    public class request
    {
        public string mid { get; set; }
        public string request_id { get; set; }
        public string ip_address { get; set; }
        public string notification_url { get; set; }
        public string response_url { get; set; }
        public string cancel_url { get; set; }
        public string mtac_url { get; set; }
        public string descriptor_note { get; set; }
        public string fname { get; set; }
        public string lname { get; set; }
        public string mname { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string country { get; set; }
        public string zip { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string mobile { get; set; }
        public string client_ip { get; set; }
        public decimal amount { get; set; }
        public string currency { get; set; }
        public string pmethod { get; set; }
        public string expiry_limit { get; set; }
        public string trxtype { get; set; }
        public string mlogo_url { get; set; }
        public Orders orders { get; set; }
        public string secure3d { get; set; }
        public string metadata2 { get; set; }
        public string signature { get; set; }

        public class Orders
        {
            public List<Items> items { get; set; }
            public class Items
            {
                public string itemname { get; set; }
                public int quantity { get; set; }
                public decimal amount { get; set; }
            }
        }
    }
}
