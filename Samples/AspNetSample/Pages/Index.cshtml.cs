using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Segment.Analytics;

namespace AspNetSample.Pages
{
    public class IndexModel : AnalyticsPageModel
    {
        public IndexModel(Analytics analytics) : base(analytics)
        {
        }

        public void OnGet()
        {
        }
    }
}
