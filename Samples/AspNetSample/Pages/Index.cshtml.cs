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
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        private readonly Analytics _analytics;

        public IndexModel(ILogger<IndexModel> logger, Analytics analytics)
        {
            _logger = logger;
            _analytics = analytics;
        }

        public void OnGet()
        {
        }
    }
}
