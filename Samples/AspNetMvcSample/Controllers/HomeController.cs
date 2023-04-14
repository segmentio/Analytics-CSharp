using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AspNetMvcSample.Models;
using AspNetMvcSample.Services;
using Segment.Analytics;

namespace AspNetMvcSample.Controllers
{
    public class HomeController : AnalyticsController
    {
        public HomeController(Analytics analytics) : base(analytics)
        {
        }

        public IActionResult Index()
        {
            return View(viewModel);
        }

        public IActionResult Pizza()
        {
            return RedirectToAction("Index", "Pizza");
        }

        public IActionResult Privacy()
        {
            return View(viewModel);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            viewModel.ErrorViewModel =
                new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier};
            return View(viewModel);
        }
    }
}
