using Grand.Core.Domain.Orders;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grand.Services.Commands.Models.Orders
{
    public class ProcessingOrderCommand : IRequest<bool>
    {
        public Order Order { get; set; }
        public bool NotifyCustomer { get; set; }
        public bool NotifyStoreOwner { get; set; } = false;
    }
}
