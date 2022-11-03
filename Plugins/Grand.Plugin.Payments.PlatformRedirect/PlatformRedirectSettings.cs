using Grand.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grand.Plugin.Payments.PlatformRedirect
{
    public class PlatformRedirectSettings : ISettings
    {
        public string DescriptionText { get; set; }
    }
}
