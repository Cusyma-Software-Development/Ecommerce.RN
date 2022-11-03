using Grand.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grand.Plugin.Payments.BankTransfer
{
    public class BankTransferSettings : ISettings
    {
        public string DescriptionText { get; set; }
    }
}
