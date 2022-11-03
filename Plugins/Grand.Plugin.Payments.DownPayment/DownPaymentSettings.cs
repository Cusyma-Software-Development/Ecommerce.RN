using Grand.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grand.Plugin.Payments.DownPayment
{
    public class DownPaymentSettings : ISettings
    {
        public string DescriptionText { get; set; }
    }
}
