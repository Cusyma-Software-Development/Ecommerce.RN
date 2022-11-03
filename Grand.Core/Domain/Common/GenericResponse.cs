using System;
using System.Collections.Generic;
using System.Text;

namespace Grand.Core.Domain.Common
{
    public class GenericResponse
    {
        public bool isSuccess { get; set; }
        public string message { get; set; }
        public int statusCode { get; set; }
    }
}
