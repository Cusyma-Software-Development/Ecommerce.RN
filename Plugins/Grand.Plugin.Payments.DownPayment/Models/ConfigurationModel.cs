using Grand.Framework.Localization;
using Grand.Framework.Mvc.ModelBinding;
using Grand.Framework.Mvc.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Grand.Plugin.Payments.DownPayment.Models
{
    public class ConfigurationModel : BaseGrandModel, ILocalizedModel<ConfigurationModel.ConfigurationLocalizedModel>
    {
        public ConfigurationModel()
        {
            Locales = new List<ConfigurationLocalizedModel>();
        }
        public string DescriptionText { get; set; }
        public bool DescriptionText_OverrideForStore { get; set; }

        public IList<ConfigurationLocalizedModel> Locales { get; set; }

        public partial class ConfigurationLocalizedModel : ILocalizedModelLocal
        {
            public string LanguageId { get; set; }

            [GrandResourceDisplayName("Plugins.Payment.DownPayment.DescriptionText")]
            public string DescriptionText { get; set; }
        }
    }
}
