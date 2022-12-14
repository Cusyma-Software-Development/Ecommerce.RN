using Grand.Core;
using Grand.Core.Data;
using Grand.Core.Domain.Catalog;
using Grand.Core.Domain.Common;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Core.Domain.Shipping;
using Grand.Services.Events;
using Grand.Services.Queries.Models.Orders;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Org.BouncyCastle.Operators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Services.Orders
{
    /// <summary>
    /// Order service
    /// </summary>
    public partial class OrderService : IOrderService
    {
        #region Fields

        private readonly IRepository<Order> _orderRepository;
        private readonly IRepository<OrderNote> _orderNoteRepository;
        private readonly IRepository<Product> _productRepository;
        private readonly IRepository<ProductDeleted> _productDeletedRepository;
        private readonly IRepository<ProductAlsoPurchased> _productAlsoPurchasedRepository;
        private readonly IRepository<RecurringPayment> _recurringPaymentRepository;
        private readonly IRepository<PaymentCollections> _paymentCollections;
        private readonly IMediator _mediator;

        #endregion

        #region Ctor

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="orderRepository">Order repository</param>
        /// <param name="orderNoteRepository">Order note repository</param>
        /// <param name="productRepository">Product repository</param>
        /// <param name="recurringPaymentRepository">Recurring payment repository</param>
        /// <param name="mediator">Mediator</param>
        /// <param name="productAlsoPurchasedRepository">Product also purchased repository</param>
        public OrderService(IRepository<Order> orderRepository,
            IRepository<OrderNote> orderNoteRepository,
            IRepository<Product> productRepository,
            IRepository<RecurringPayment> recurringPaymentRepository,
            IMediator mediator,
            IRepository<ProductAlsoPurchased> productAlsoPurchasedRepository,
            IRepository<ProductDeleted> productDeletedRepository,
            IRepository<PaymentCollections> paymentCollections)
        {
            _orderRepository = orderRepository;
            _orderNoteRepository = orderNoteRepository;
            _productRepository = productRepository;
            _recurringPaymentRepository = recurringPaymentRepository;
            _mediator = mediator;
            _productAlsoPurchasedRepository = productAlsoPurchasedRepository;
            _productDeletedRepository = productDeletedRepository;
            _paymentCollections = paymentCollections;
        }

        #endregion

        #region Methods

        #region Orders

        /// <summary>
        /// Gets an order
        /// </summary>
        /// <param name="orderId">The order identifier</param>
        /// <returns>Order</returns>
        public virtual Task<Order> GetOrderById(string orderId)
        {
            return _orderRepository.GetByIdAsync(orderId);
        }


        /// <summary>
        /// Gets an order
        /// </summary>
        /// <param name="orderId">The order item identifier</param>
        /// <returns>Order</returns>
        public virtual Task<Order> GetOrderByOrderItemId(string orderItemId)
        {
            var query = from o in _orderRepository.Table
                        where o.OrderItems.Any(x => x.Id == orderItemId)
                        select o;

            return query.FirstOrDefaultAsync();
        }
        /// <summary>
        /// Gets an order
        /// </summary>
        /// <param name="orderNumber">The order number</param>
        /// <returns>Order</returns>
        public virtual Task<Order> GetOrderByNumber(int orderNumber)
        {
            return _orderRepository.Table.FirstOrDefaultAsync(x => x.OrderNumber == orderNumber);
        }


        /// <summary>
        /// Get orders by identifiers
        /// </summary>
        /// <param name="orderIds">Order identifiers</param>
        /// <returns>Order</returns>
        public virtual async Task<IList<Order>> GetOrdersByIds(string[] orderIds)
        {
            if (orderIds == null || orderIds.Length == 0)
                return new List<Order>();

            var query = from o in _orderRepository.Table
                        where orderIds.Contains(o.Id)
                        select o;
            var orders = await query.ToListAsync();
            //sort by passed identifiers
            var sortedOrders = new List<Order>();
            foreach (string id in orderIds)
            {
                var order = orders.Find(x => x.Id == id);
                if (order != null)
                    sortedOrders.Add(order);
            }
            return sortedOrders;
        }

        /// <summary>
        /// Gets an order
        /// </summary>
        /// <param name="orderGuid">The order identifier</param>
        /// <returns>Order</returns>
        public virtual Task<Order> GetOrderByGuid(Guid orderGuid)
        {
            var query = from o in _orderRepository.Table
                        where o.OrderGuid == orderGuid
                        select o;
            return query.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Deletes an order
        /// </summary>
        /// <param name="order">The order</param>
        public virtual async Task DeleteOrder(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            var filters = Builders<ProductAlsoPurchased>.Filter;
            var filter = filters.Where(x => x.OrderId == order.Id);
            order.Deleted = true;
            await UpdateOrder(order);

            //delete product also purchased
            await _productAlsoPurchasedRepository.Collection.DeleteManyAsync(filter);

        }

        /// <summary>
        /// Search orders
        /// </summary>
        /// <param name="storeId">Store identifier; 0 to load all orders</param>
        /// <param name="vendorId">Vendor identifier; null to load all orders</param>
        /// <param name="customerId">Customer identifier; 0 to load all orders</param>
        /// <param name="productId">Product identifier which was purchased in an order; 0 to load all orders</param>
        /// <param name="affiliateId">Affiliate identifier; 0 to load all orders</param>
        /// <param name="billingCountryId">Billing country identifier; 0 to load all orders</param>
        /// <param name="warehouseId">Warehouse identifier, only orders with products from a specified warehouse will be loaded; 0 to load all orders</param>
        /// <param name="paymentMethodSystemName">Payment method system name; null to load all records</param>
        /// <param name="createdFromUtc">Created date from (UTC); null to load all records</param>
        /// <param name="createdToUtc">Created date to (UTC); null to load all records</param>
        /// <param name="os">Order status; null to load all orders</param>
        /// <param name="ps">Order payment status; null to load all orders</param>
        /// <param name="ss">Order shipment status; null to load all orders</param>
        /// <param name="billingEmail">Billing email. Leave empty to load all records.</param>
        /// <param name="orderNotes">Search in order notes. Leave empty to load all records.</param>
        /// <param name="orderGuid">Search by order GUID (Global unique identifier) or part of GUID. Leave empty to load all orders.</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Orders</returns>
        public virtual async Task<IPagedList<Order>> SearchOrders(string storeId = "",
            string vendorId = "", string customerId = "",
            string productId = "", string affiliateId = "", string warehouseId = "",
            string billingCountryId = "", string paymentMethodSystemName = null,
            DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
            OrderStatus? os = null, PaymentStatus? ps = null, ShippingStatus? ss = null,
            string billingEmail = null, string billingLastName = "", string orderGuid = null,
            int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var querymodel = new GetOrderQuery() {
                AffiliateId = affiliateId,
                BillingCountryId = billingCountryId,
                BillingEmail = billingEmail,
                BillingLastName = billingLastName,
                CreatedFromUtc = createdFromUtc,
                CreatedToUtc = createdToUtc,
                CustomerId = customerId,
                OrderGuid = orderGuid,
                Os = os,
                PageIndex = pageIndex,
                PageSize = pageSize,
                PaymentMethodSystemName = paymentMethodSystemName,
                ProductId = productId,
                Ps = ps,
                Ss = ss,
                StoreId = storeId,
                VendorId = vendorId,
                WarehouseId = warehouseId
            };
            var query = await _mediator.Send(querymodel);
            return await PagedList<Order>.Create(query, pageIndex, pageSize);
        }

        /// <summary>
        /// Inserts an order
        /// </summary>
        /// <param name="order">Order</param>
        public virtual async Task InsertOrder(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            var orderExists = _orderRepository.Table.OrderByDescending(x => x.OrderNumber).Select(x => x.OrderNumber).FirstOrDefault();
            order.OrderNumber = orderExists != 0 ? orderExists + 1 : 1;
            
            await _orderRepository.InsertAsync(order);

            //event notification
            await _mediator.EntityInserted(order);
        }

        public virtual async Task AddPaymentColection(PaymentCollections paymentCollections)
        {
            if (paymentCollections == null)
                throw new ArgumentNullException("order");
            await _paymentCollections.InsertAsync(paymentCollections);
            await _mediator.EntityInserted(paymentCollections);
        }

        public virtual async Task<List<PaymentCollections>> GetPaymentCollections(DateTime dateStart, DateTime dateEnd)
        {
            return await _paymentCollections.Collection.Find(f => f.PaymentDate >= dateStart && f.PaymentDate <= dateEnd).ToListAsync<PaymentCollections>();
        }

        public virtual async Task<List<PaymentCollections>> GetPaymentCollectionsByOrder(Guid orderId)
        {
            return await _paymentCollections.Collection.Find(f => f.OrderId == orderId).ToListAsync<PaymentCollections>();
        }

        /// <summary>
        /// Inserts an product also purchased
        /// </summary>
        /// <param name="order">Order</param>
        public virtual async Task InsertProductAlsoPurchased(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            foreach (var item in order.OrderItems)
            {
                foreach (var it in order.OrderItems.Where(x => x.ProductId != item.ProductId))
                {
                    var productPurchase = new ProductAlsoPurchased();
                    productPurchase.ProductId = item.ProductId;
                    productPurchase.OrderId = order.Id;
                    productPurchase.CreatedOrderOnUtc = order.CreatedOnUtc;
                    productPurchase.Quantity = it.Quantity;
                    productPurchase.StoreId = order.StoreId;
                    productPurchase.ProductId2 = it.ProductId;
                    await _productAlsoPurchasedRepository.InsertAsync(productPurchase);
                }
            }
        }

        /// <summary>
        /// Updates the order
        /// </summary>
        /// <param name="order">The order</param>
        public virtual async Task UpdateOrder(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            await _orderRepository.UpdateAsync(order);

            //event notification
            await _mediator.EntityUpdated(order);
        }

        /// <summary>
        /// Get an order by authorization transaction ID and payment method system name
        /// </summary>
        /// <param name="authorizationTransactionId">Authorization transaction ID</param>
        /// <param name="paymentMethodSystemName">Payment method system name</param>
        /// <returns>Order</returns>
        public virtual Task<Order> GetOrderByAuthorizationTransactionIdAndPaymentMethod
            (string authorizationTransactionId,
            string paymentMethodSystemName)
        {
            var query = _orderRepository.Table;

            if (!string.IsNullOrWhiteSpace(authorizationTransactionId))
                query = query.Where(o => o.AuthorizationTransactionId == authorizationTransactionId);

            if (!string.IsNullOrWhiteSpace(paymentMethodSystemName))
                query = query.Where(o => o.PaymentMethodSystemName == paymentMethodSystemName);

            query = query.OrderByDescending(o => o.CreatedOnUtc);
            return query.FirstOrDefaultAsync();
        }

        #endregion

        #region Orders items

        /// <summary>
        /// Gets an item
        /// </summary>
        /// <param name="orderItemGuid">Order identifier</param>
        /// <returns>Order item</returns>
        public virtual Task<OrderItem> GetOrderItemByGuid(Guid orderItemGuid)
        {
            var query = from order in _orderRepository.Table
                        from orderItem in order.OrderItems
                        select orderItem;

            query = from orderItem in query
                    where orderItem.OrderItemGuid == orderItemGuid
                    select orderItem;

            return query.FirstOrDefaultAsync();
        }

        public virtual async Task<IList<Order>> GetOrderByPeriod(DateTime startDate, DateTime endDate)
        {
            return await _orderRepository.Table.Where(w => w.CreatedOnUtc >= startDate && w.CreatedOnUtc <= endDate).ToListAsync();
        }

        /// <summary>
        /// Gets all order items
        /// </summary>
        /// <param name="orderId">Order identifier; null to load all records</param>
        /// <param name="customerId">Customer identifier; null to load all records</param>
        /// <param name="createdFromUtc">Order created date from (UTC); null to load all records</param>
        /// <param name="createdToUtc">Order created date to (UTC); null to load all records</param>
        /// <param name="os">Order status; null to load all records</param>
        /// <param name="ps">Order payment status; null to load all records</param>
        /// <param name="ss">Order shipment status; null to load all records</param>
        /// <param name="loadDownloableProductsOnly">Value indicating whether to load downloadable products only</param>
        /// <returns>Orders</returns>
        public virtual async Task<IList<OrderItem>> GetAllOrderItems(string orderId,
            string customerId, DateTime? createdFromUtc, DateTime? createdToUtc,
            OrderStatus? os, PaymentStatus? ps, ShippingStatus? ss,
            bool loadDownloableProductsOnly)
        {
            var querymodel = new GetOrderQuery() {
                CreatedFromUtc = createdFromUtc,
                CreatedToUtc = createdToUtc,
                CustomerId = customerId,
                Os = os,
                OrderId = orderId,
                Ps = ps,
                Ss = ss,
            };
            var query = await _mediator.Send(querymodel);
            var items = new List<OrderItem>();
            foreach (var order in await query.ToListAsync())
            {
                foreach (var orderitem in order.OrderItems)
                {
                    if (loadDownloableProductsOnly)
                    {
                        var product = await _productRepository.GetByIdAsync(orderitem.ProductId);
                        if (product == null)
                            product = await _productDeletedRepository.GetByIdAsync(orderitem.ProductId) as Product;
                        if (product != null)
                        {
                            if (product.IsDownload)
                            {
                                items.Add(orderitem);
                            }
                        }
                    }
                    else
                        items.Add(orderitem);
                }

            }
            return items;
        }

        /// <summary>
        /// Delete an order item
        /// </summary>
        /// <param name="orderItem">The order item</param>
        public virtual async Task DeleteOrderItem(OrderItem orderItem)
        {
            if (orderItem == null)
                throw new ArgumentNullException("orderItem");

            var order = await GetOrderByOrderItemId(orderItem.Id);

            var updatebuilder = Builders<Order>.Update;
            var updatefilter = updatebuilder.PullFilter(x => x.OrderItems, y => y.Id == orderItem.Id);
            await _orderRepository.Collection.UpdateOneAsync(new BsonDocument("_id", order.Id), updatefilter);

            var filters = Builders<ProductAlsoPurchased>.Filter;
            var filter = filters.Where(x => x.OrderId == order.Id && (x.ProductId == orderItem.ProductId || x.ProductId2 == orderItem.ProductId));
            await _productAlsoPurchasedRepository.Collection.DeleteManyAsync(filter);

            //event notification
            await _mediator.EntityDeleted(orderItem);
        }

        #endregion

        #region Orders notes

        /// <summary>
        /// Deletes an order note
        /// </summary>
        /// <param name="orderNote">The order note</param>
        public virtual async Task DeleteOrderNote(OrderNote orderNote)
        {
            if (orderNote == null)
                throw new ArgumentNullException("orderNote");

            await _orderNoteRepository.DeleteAsync(orderNote);

            //event notification
            await _mediator.EntityDeleted(orderNote);
        }

        /// <summary>
        /// Deletes an order note
        /// </summary>
        /// <param name="orderNote">The order note</param>
        public virtual async Task InsertOrderNote(OrderNote orderNote)
        {
            if (orderNote == null)
                throw new ArgumentNullException("orderNote");

            await _orderNoteRepository.InsertAsync(orderNote);

            //event notification
            await _mediator.EntityInserted(orderNote);
        }

        public virtual async Task<IList<OrderNote>> GetOrderNotes(string orderId)
        {
            var query = from orderNote in _orderNoteRepository.Table
                        where orderNote.OrderId == orderId
                        orderby orderNote.CreatedOnUtc descending
                        select orderNote;

            return await query.ToListAsync();
        }

        /// <summary>
        /// Get ordernote by id
        /// </summary>
        /// <param name="ordernoteId">Order note identifier</param>
        /// <returns>OrderNote</returns>
        public virtual Task<OrderNote> GetOrderNote(string ordernoteId)
        {
            return _orderNoteRepository.Table.Where(x => x.Id == ordernoteId).FirstOrDefaultAsync();
        }


        #endregion

        #region Recurring payments

        /// <summary>
        /// Deletes a recurring payment
        /// </summary>
        /// <param name="recurringPayment">Recurring payment</param>
        public virtual async Task DeleteRecurringPayment(RecurringPayment recurringPayment)
        {
            if (recurringPayment == null)
                throw new ArgumentNullException("recurringPayment");

            recurringPayment.Deleted = true;
            await UpdateRecurringPayment(recurringPayment);
        }

        /// <summary>
        /// Gets a recurring payment
        /// </summary>
        /// <param name="recurringPaymentId">The recurring payment identifier</param>
        /// <returns>Recurring payment</returns>
        public virtual Task<RecurringPayment> GetRecurringPaymentById(string recurringPaymentId)
        {
            return _recurringPaymentRepository.GetByIdAsync(recurringPaymentId);
        }

        /// <summary>
        /// Inserts a recurring payment
        /// </summary>
        /// <param name="recurringPayment">Recurring payment</param>
        public virtual async Task InsertRecurringPayment(RecurringPayment recurringPayment)
        {
            if (recurringPayment == null)
                throw new ArgumentNullException("recurringPayment");

            await _recurringPaymentRepository.InsertAsync(recurringPayment);

            //event notification
            await _mediator.EntityInserted(recurringPayment);
        }

        /// <summary>
        /// Updates the recurring payment
        /// </summary>
        /// <param name="recurringPayment">Recurring payment</param>
        public virtual async Task UpdateRecurringPayment(RecurringPayment recurringPayment)
        {
            if (recurringPayment == null)
                throw new ArgumentNullException("recurringPayment");

            await _recurringPaymentRepository.UpdateAsync(recurringPayment);

            //event notification
            await _mediator.EntityUpdated(recurringPayment);
        }

        /// <summary>
        /// Search recurring payments
        /// </summary>
        /// <param name="storeId">The store identifier; "" to load all records</param>
        /// <param name="customerId">The customer identifier; "" to load all records</param>
        /// <param name="initialOrderId">The initial order identifier; "" to load all records</param>
        /// <param name="initialOrderStatus">Initial order status identifier; null to load all records</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <returns>Recurring payments</returns>
        public virtual async Task<IPagedList<RecurringPayment>> SearchRecurringPayments(string storeId = "",
            string customerId = "", string initialOrderId = "", OrderStatus? initialOrderStatus = null,
            int pageIndex = 0, int pageSize = int.MaxValue, bool showHidden = false)
        {
            int? initialOrderStatusId = null;
            if (initialOrderStatus.HasValue)
                initialOrderStatusId = (int)initialOrderStatus.Value;
            //TO DO
            var query1 = from rp in _recurringPaymentRepository.Table
                         where
                         (!rp.Deleted) &&
                         (showHidden || rp.IsActive) &&
                         (customerId == "" || rp.InitialOrder.CustomerId == customerId) &&
                         (storeId == "" || rp.InitialOrder.StoreId == storeId) &&
                         (initialOrderId == "" || rp.InitialOrder.Id == initialOrderId)
                         select rp.Id;
            var cc = query1.ToList();
            var query2 = from rp in _recurringPaymentRepository.Table
                         where cc.Contains(rp.Id)
                         orderby rp.StartDateUtc
                         select rp;
            return await PagedList<RecurringPayment>.Create(query2, pageIndex, pageSize);
        }

        public virtual async Task<GenericResponse> ApplyPaymentToOrder(int orderId,decimal paymentAmount,Guid orderGuid,string paymentType,string reference,string remarks,string attachment,string attachmentFile)
        {
            reference = String.IsNullOrEmpty(reference) ? "" : reference;
            remarks = String.IsNullOrEmpty(remarks) ? "" : remarks;
            attachment = String.IsNullOrEmpty(attachment) ? "" : attachment;
            attachmentFile = String.IsNullOrEmpty(attachmentFile) ? "" : attachmentFile;

            GenericResponse response = new GenericResponse();
            try
            {
                bool isApplied = false;
                var order = await GetOrderByGuid(orderGuid);
                if (order != null)
                {
                    if (order.Balance <= 0)
                        return response = new GenericResponse {
                            isSuccess = false,
                            message = "Order is already fully paid",
                            statusCode = 500
                        };

                    if (paymentAmount > order.Balance)
                        return response = new GenericResponse {
                            isSuccess = false,
                            message = "Your payment exceeds the balance remaining of the order",
                            statusCode = 500
                        };

                    //Apply payment to collections record
                    await AddPaymentColection(new PaymentCollections {
                        Active = true,
                        AmountPaid = paymentAmount,
                        Id = Guid.NewGuid().ToString(),
                        OrderId = orderGuid,
                        OrderNumber = orderId,
                        PaymentDate = DateTime.UtcNow,
                        PaymentId = Guid.NewGuid(),
                        PaymentType = paymentType,
                        Attachment = attachment,
                        Reference = reference,
                        Remarks = remarks,
                        FileName = attachmentFile
                    });

                    order.Balance = order.Balance - paymentAmount;
                    order.AmountPaid += paymentAmount;
                    if (order.Balance <= 0)
                    {
                        order.PaymentStatusId = (int)PaymentStatus.Paid;
                        order.PaidDateUtc = DateTime.UtcNow;
                    }
                    _orderRepository.Update(order);
                    if(order.PaymentStatusId == (int)PaymentStatus.Paid)
                    {
                        await InsertOrderNote(new OrderNote {
                            Note = "Order has been marked as paid",
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow,
                            OrderId = order.Id,

                        });
                    }
                    

                    response = new GenericResponse {
                        isSuccess = true,
                        message = "Your payment has been applied successfully",
                        statusCode = 200
                    };

                }
            }
            catch(Exception ex)
            {
                response = new GenericResponse {
                    isSuccess = false,
                    message = ex.ToString(),
                    statusCode = 500
                };
            }
            return response;
        }

        #endregion

        #endregion      
    }
}
