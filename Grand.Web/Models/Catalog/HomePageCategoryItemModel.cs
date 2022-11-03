using Grand.Framework.Mvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grand.Web.Models.Catalog
{
    public class HomePageCategoryItemModel : BaseGrandModel
    {
        public IEnumerable<Models.Catalog.ProductOverviewModel> products { get; set; }
        public string CategoryName { get; set; }
    }
}
