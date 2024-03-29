﻿using Grand.Core.Domain.Orders;
using Grand.Services.Catalog;
using Grand.Services.Commands.Models.Orders;
using Grand.Services.Discounts;
using Grand.Services.Orders;
using Grand.Services.Shipping;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Grand.Services.Commands.Handlers.Orders
{
    public class ProcessingOrderCommandHandler : IRequestHandler<ProcessingOrderCommand, bool>
    {
        private readonly IMediator _mediator;
        private readonly IOrderService _orderService;
        private readonly IShipmentService _shipmentService;
        private readonly IProductService _productService;
        private readonly IProductReservationService _productReservationService;
        private readonly IAuctionService _auctionService;
        private readonly IDiscountService _discountService;

        public ProcessingOrderCommandHandler(
            IMediator mediator,
            IOrderService orderService,
            IShipmentService shipmentService,
            IProductService productService,
            IProductReservationService productReservationService,
            IAuctionService auctionService,
            IDiscountService discountService)
        {
            _mediator = mediator;
            _orderService = orderService;
            _shipmentService = shipmentService;
            _productService = productService;
            _productReservationService = productReservationService;
            _auctionService = auctionService;
            _discountService = discountService;
        }

        public async Task<bool> Handle(ProcessingOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.Order == null)
                throw new ArgumentNullException("order");


            //Cancel order
         

            //add a note
            await _orderService.InsertOrderNote(new OrderNote {
                Note = "Order has been cancelled",
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow,
                OrderId = request.Order.Id,

            });

            //return (add) back redeemded reward points
            await _mediator.Send(new ReturnBackRedeemedRewardPointsCommand() { Order = request.Order });

            //cancel recurring payments
            var recurringPayments = await _orderService.SearchRecurringPayments(initialOrderId: request.Order.Id);
            foreach (var rp in recurringPayments)
            {
                var errors = await _mediator.Send(new CancelRecurringPaymentCommand() { RecurringPayment = rp });
            }

            //Adjust inventory for already shipped shipments
            //only products with "use multiple warehouses"
            var shipments = await _shipmentService.GetShipmentsByOrder(request.Order.Id);
            foreach (var shipment in shipments)
            {
                foreach (var shipmentItem in shipment.ShipmentItems)
                {
                    var product = await _productService.GetProductById(shipmentItem.ProductId);
                    shipmentItem.ShipmentId = shipment.Id;
                    await _productService.ReverseBookedInventory(product, shipment, shipmentItem);
                }
            }
            //Adjust inventory
            foreach (var orderItem in request.Order.OrderItems)
            {
                var product = await _productService.GetProductById(orderItem.ProductId);
                await _productService.AdjustInventory(product, orderItem.Quantity, orderItem.AttributesXml, orderItem.WarehouseId);
            }

            //cancel reservations
            await _productReservationService.CancelReservationsByOrderId(request.Order.Id);

            //cancel bid
            await _auctionService.CancelBidByOrder(request.Order.Id);

            //cancel discount
            await _discountService.CancelDiscount(request.Order.Id);

            //event notification
            await _mediator.Publish(new OrderCancelledEvent(request.Order));

            return true;
        }
    }
}
