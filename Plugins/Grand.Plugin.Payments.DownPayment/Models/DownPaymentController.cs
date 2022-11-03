using Grand.Core;
using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Plugin.Payments.DownPayment.Models;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Stores;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.DownPayment.Controllers
{
    [Area("Admin")]
    [AuthorizeAdmin]
    public class DownPaymentController :  BasePluginController
    {
        private readonly DownPaymentSettings _downPaymentSettings;
        private readonly ISettingService _settingsService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;


        public DownPaymentController(DownPaymentSettings downPaymentSettings,ISettingService settingService,ILocalizationService localizationService,IStoreService storeService,IWorkContext workContext)
        {
            this._downPaymentSettings = downPaymentSettings;
            this._settingsService = settingService;
            this._localizationService = localizationService;
            this._storeService = storeService;
            this._workContext = workContext;
        }

        public async Task<IActionResult> Configure()
        {
            //load settings for a chosen store scope
            var storeScope = await this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var downPaymentSettings = _settingsService.LoadSetting<DownPaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.DescriptionText = downPaymentSettings.DescriptionText;
            return View(model);
        }

        [HttpPost]
        public async  Task<IActionResult> Configure(ConfigurationModel model)
        {
            _settingsService.SaveSetting(_downPaymentSettings);
            _settingsService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            return await Configure();
        }
    }
}
