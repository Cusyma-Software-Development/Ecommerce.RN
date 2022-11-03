using Grand.Core;
using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Plugin.Payments.Paynamics.Models;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Stores;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.Paynamics.Controllers
{
    [Area("Admin")]
    [AuthorizeAdmin]
    public class PaynamicsController : BasePluginController
    {
        private readonly PaynamicsSettings _PaynamicsSettings;
        private readonly ISettingService _settingsService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;


        public PaynamicsController(PaynamicsSettings PaynamicsSettings, ISettingService settingService, ILocalizationService localizationService, IStoreService storeService, IWorkContext workContext)
        {
            this._PaynamicsSettings = PaynamicsSettings;
            this._settingsService = settingService;
            this._localizationService = localizationService;
            this._storeService = storeService;
            this._workContext = workContext;
        }

        public async Task<IActionResult> Configure()
        {
            //load settings for a chosen store scope
            var storeScope = await this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var downPaymentSettings = _settingsService.LoadSetting<PaynamicsSettings>(storeScope);

            var model = new ConfigurationModel();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            _settingsService.SaveSetting(_PaynamicsSettings);
            _settingsService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            return await Configure();
        }
    }
}
