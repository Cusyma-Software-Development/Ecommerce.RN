using Amazon.S3;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Services.Customers;
using Grand.Services.Directory;
using Grand.Services.Orders;
using Grand.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Controllers
{
    [AllowAnonymous]
    public class SyncDataController : BasePublicController
    {
        private readonly IOrderService _orderService;
        private readonly ICustomerService _customerService;
        private readonly IStateProvinceService _stateProvinceService;
        //AmazonS3Service s3Service = new AmazonS3Service();
        IAmazonS3 _s3Client { get; set; }

        public SyncDataController(IOrderService orderService, ICustomerService customerService, IStateProvinceService stateProvinceService)
        {
            _orderService = orderService;
            _customerService = customerService;
            //_s3Client = s3Client;
            _stateProvinceService = stateProvinceService;
        }


        [HttpGet]
        public async Task<List<Core.Domain.Directory.StateProvince>> GetProvinceList()
        {
            var provinces = await _stateProvinceService.GetStateProvinces();
            return provinces.ToList();
        }

        //[HttpGet]
        //public async Task<List<PaymentCollectionObject>> GetPaymentCollections(string startDate, string endDate)
        //{
        //    startDate = String.IsNullOrEmpty(startDate) ? DateTime.Now.ToShortDateString() + " 00:00" : Convert.ToDateTime(startDate).ToShortDateString() + " 00:00";
        //    endDate = String.IsNullOrEmpty(endDate) ? DateTime.Now.ToShortDateString() + " 23:59" : Convert.ToDateTime(endDate).ToShortDateString() + " 23:59";
        //    return await _orderService.GetPaymentOrder(Convert.ToDateTime(startDate), Convert.ToDateTime(endDate));
        //}

        //[HttpGet]
        //public async Task<IActionResult> UpdateBase64Collections(string startDate, string endDate)
        //{
        //    startDate = String.IsNullOrEmpty(startDate) ? DateTime.Now.ToShortDateString() + " 00:00" : Convert.ToDateTime(startDate).ToShortDateString() + " 00:00";
        //    endDate = String.IsNullOrEmpty(endDate) ? DateTime.Now.ToShortDateString() + " 23:59" : Convert.ToDateTime(endDate).ToShortDateString() + " 23:59";
        //    List<PaymentCollectionObject> paymentCollectionObjects = await _orderService.GetPaymentOrder(Convert.ToDateTime(startDate), Convert.ToDateTime(endDate));
        //    paymentCollectionObjects.ForEach(fe =>
        //    {
        //        if (fe.Attachment.Contains("base64"))
        //        {
        //            AmazonS3Service.UploadStatusResponse uploadObject = s3Service.UploadObjectBase64(fe.Attachment).Result;
        //            List<Grand.Web.Areas.Admin.Models.Common.FileObjectModel> fileObjects = new List<Areas.Admin.Models.Common.FileObjectModel>();
        //            if (uploadObject.Success)
        //            {
        //                fileObjects.Add(new Areas.Admin.Models.Common.FileObjectModel {
        //                    fileName = fe.FileName,
        //                    url = uploadObject.Url,
        //                    originalFileName = fe.FileName
        //                });

        //                string serializedFileObject = Newtonsoft.Json.JsonConvert.SerializeObject(fileObjects);
        //                bool successUpdate = _orderService.UpdateFilePaymentCollections(serializedFileObject, fe.PaymentId).Result;
        //                if (successUpdate)
        //                {

        //                }
        //            }
        //        }
        //    });
        //    return StatusCode(200);
        //}

        [HttpGet]
        public async Task<List<Order>> GetOrders(string startDate, string endDate)
        {
            startDate = String.IsNullOrEmpty(startDate) ? DateTime.Now.ToShortDateString() + " 00:00" : Convert.ToDateTime(startDate).ToShortDateString() + " 00:00";
            endDate = String.IsNullOrEmpty(endDate) ? DateTime.Now.ToShortDateString() + " 23:59" : Convert.ToDateTime(endDate).ToShortDateString() + " 23:59";
            var orders = await _orderService.GetOrderByPeriod(Convert.ToDateTime(startDate), Convert.ToDateTime(endDate));
            return orders.ToList();
        }

        [HttpGet]
        public async Task<List<Customer>> GetCustomerRecords(string startDate, string endDate)
        {
            startDate = String.IsNullOrEmpty(startDate) ? DateTime.Now.ToShortDateString() + " 00:00" : Convert.ToDateTime(startDate).ToShortDateString() + " 00:00";
            endDate = String.IsNullOrEmpty(endDate) ? DateTime.Now.ToShortDateString() + " 23:59" : Convert.ToDateTime(endDate).ToShortDateString() + " 23:59";
            var customer = await _customerService.GetAllCustomers(Convert.ToDateTime(startDate), Convert.ToDateTime(endDate));
            return customer.ToList();
        }
    }
}