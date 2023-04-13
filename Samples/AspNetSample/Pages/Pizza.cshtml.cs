using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetSample.Pages.Models;
using AspNetSample.Pages.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetSample.Pages
{
    public class PizzaModel : PageModel
    {
        [BindProperty]
        public Pizza NewPizza { get; set; } = new();

        public List<Pizza> pizzas = new();

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
            return RedirectToAction("Get");
        }

        public IActionResult OnPostDelete(int id)
        {
            PizzaService.Delete(id);
            return RedirectToAction("Get");
        }

    }
}
