using AspNetMvcSample.Models;
using AspNetMvcSample.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Segment.Analytics;
using Segment.Serialization;

namespace AspNetMvcSample.Controllers
{
    public class PizzaController : AnalyticsController
    {
        public PizzaController(Analytics analytics) : base(analytics)
        {
        }

        public IActionResult Index()
        {
            viewModel.pizzas = PizzaService.GetAll();
            return View(viewModel);
        }

        [HttpPost]
        public IActionResult OnPost(Pizza pizza)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Index");
            }
            PizzaService.Add(pizza);
            analytics.Track("New Pizza Added", new JsonObject
            {
                ["id"] = pizza.Id,
                ["isGlutenFree"] = pizza.IsGlutenFree,
                ["name"] = pizza.Name,
                ["price"] = (double)pizza.Price,
                ["size"] = (int)pizza.Size
            });

            return RedirectToAction("Index");
        }

        public IActionResult OnPostDelete(int id)
        {
            PizzaService.Delete(id);
            analytics.Track("Pizza Deleted", new JsonObject
            {
                ["id"] = id
            });
            return RedirectToAction("Index");
        }
    }
}
