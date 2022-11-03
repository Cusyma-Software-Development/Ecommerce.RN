using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Core.Plugins;
using Grand.Services.Localization;
using Grand.Services.Payments;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grand.Plugin.Payments.PlatformRedirect
{
    public class PlatformRedirect : BasePlugin, IPaymentMethod
    {
        private readonly ILocalizationService _localizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public PlatformRedirect(ILocalizationService localizationService, IHttpContextAccessor httpContextAccessor)
        {
            _localizationService = localizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        public override async Task Install()
        {
            await base.Install();
        }

        public override async Task Uninstall()
        {
            await base.Uninstall();
        }

        public async Task<ProcessPaymentResult> ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return await Task.FromResult(result);
        }

        public async Task PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {

            _httpContextAccessor.HttpContext.Response.Redirect(postProcessPaymentRequest.RedirectURL);
        }

        public async Task<bool> HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return await Task.FromResult(false);
        }

        public async Task<decimal> GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0;
        }

        public async Task<CapturePaymentResult> Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return await Task.FromResult(result);
        }

        public async Task<RefundPaymentResult> Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund not supported");
            return await Task.FromResult(result);
        }

        public async Task<VoidPaymentResult> Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public async Task<ProcessPaymentResult> ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public async Task<CancelRecurringPaymentResult> CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public async Task<bool> CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //it's not a redirection payment method. So we always return false
            return await Task.FromResult(true);
        }

        public async Task<IList<string>> ValidatePaymentForm(IFormCollection form)
        {
            return await Task.FromResult(new List<string>());
        }

        public async Task<ProcessPaymentRequest> GetPaymentInfo(IFormCollection form)
        {
            ProcessPaymentRequest request = new ProcessPaymentRequest();
            return await Task.FromResult(request);
        }

        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "PlatformRedirect";
        }

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public async Task<bool> SupportCapture()
        {
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public async Task<bool> SupportPartiallyRefund()
        {
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public async Task<bool> SupportRefund()
        {
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public async Task<bool> SupportVoid()
        {
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public async Task<bool> SkipPaymentInfo()
        {
            return await Task.FromResult(false);
        }

        public async Task<string> PaymentMethodDescription()
        {
            return await Task.FromResult(_localizationService.GetResource("Plugins.Payment.PlatformRedirect.PaymentMethodDescription"));
        }
    }

}

