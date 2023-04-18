using System.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Segment.Analytics;

namespace AspNetMvcSample.Controllers
{
    public class AnalyticsController : Controller
    {
        public readonly Analytics analytics;

        public dynamic viewModel = new ExpandoObject();

        public AnalyticsController(Analytics analytics)
        {
            this.analytics = analytics;
            viewModel.analytics = this.analytics;
        }
    }
}
