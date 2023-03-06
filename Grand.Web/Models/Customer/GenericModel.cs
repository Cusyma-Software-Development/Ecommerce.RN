using Grand.Core.Domain.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Threading.Tasks;

namespace Grand.Web.Models.Customer
{
    public class GenericModel
    {
        public class RegistrationModel
        {
            public string username { get; set; }
            public string email { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string password { get; set; }
            public string gender { get; set; }
        }

        public class UpdateCustomerProfile
        {
            public string reference { get; set; } = "";
            public string gender { get; set; } = "";
        }

        public class RegisterBatchResult
        {
            public string email { get; set; }
            public string id { get; set; }
        }

        public class Result
        {
            public int StatusCode { get; set; }
            public bool IsSuccess { get; set; }
            public string Message { get; set; }
            public object ResponseObject { get; set; }
            public List<string> Errors { get; set; }

            public Result()
            {
                StatusCode = 200;
                IsSuccess = false;
                Message = "";
                Errors = new List<string>();
            }
        }
    }
}