using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grand.Core.Domain.Payments
{
    public partial class PaymentCollections : BaseEntity
    {

        public Guid PaymentId { get; set; }
        public Guid OrderId { get; set; }
        public int PaymentNumber { get; set; }
        public int OrderNumber { get; set; }

        [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
        public decimal AmountPaid { get; set; }
        public string PaymentType { get; set; }
        public DateTime PaymentDate { get; set; }
        public string FileName { get; set; }
        public string Reference { get; set; }
        public string Attachment { get; set; }
        public string Remarks { get; set; }

        public bool Active { get; set; }
    }
}
