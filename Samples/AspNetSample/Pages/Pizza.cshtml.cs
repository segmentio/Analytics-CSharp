using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetSample.Pages.Models;
using AspNetSample.Pages.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Segment.Analytics;
using Segment.Serialization;

namespace AspNetSample.Pages
{
    public class PizzaModel : AnalyticsPageModel
    {
        [BindProperty]
        public Pizza NewPizza { get; set; } = new();

        public List<Pizza> pizzas = new();

        public PizzaModel(Analytics analytics) : base(analytics)
        {
        }

        public void OnGet()
        {
            pizzas = PizzaService.GetAll();
        }

        public string GlutenFreeText(Pizza pizza)
        {
            return pizza.IsGlutenFree ? "Gluten Free": "Not Gluten Free";
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            PizzaService.Add(NewPizza);
            analytics.Track("New Pizza Added", new JsonObject
            {
                ["id"] = NewPizza.Id,
                ["isGlutenFree"] = NewPizza.IsGlutenFree,
                ["name"] = NewPizza.Name,
                ["price"] = (double)NewPizza.Price,
                ["size"] = (int)NewPizza.Size
            });
            return RedirectToAction("Get");
        }

        public IActionResult OnPostDelete(int id)
        {
            PizzaService.Delete(id);
            analytics.Track("Pizza Deleted", new JsonObject
            {
                ["id"] = id
            });
            return RedirectToAction("Get");
        }
    }
}
