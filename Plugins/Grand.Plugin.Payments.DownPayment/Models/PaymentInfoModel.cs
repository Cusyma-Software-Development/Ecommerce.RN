using Grand.Framework.Mvc.ModelBinding;
using Grand.Framework.Mvc.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Grand.Plugin.Payments.DownPayment.Models
{
    public class PaymentInfoModel : BaseGrandModel
    {
        public decimal DownPaymentAmount { get; set; }

        public string AccountName { get; set; }

        public string AttachmentName { get; set; }
        public string Attachment { get; set; }
        public string AttachmentFile { get; set; }
    }
}
