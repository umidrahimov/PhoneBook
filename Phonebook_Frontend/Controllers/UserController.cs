using Microsoft.AspNetCore.Mvc;
using Phonebook_Frontend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Phonebook_Frontend.Controllers
{
    public class UserController : Controller
    {
        public UserController()
        {
        }
        public IActionResult GetAllStudents()
        {
            IEnumerable<UserViewModel> users = null;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://backend-app-service:8080/user/");
                //HTTP GET
                var responseTask = client.GetAsync("list");
                responseTask.Wait();

                var result = responseTask.Result;
                if (result.IsSuccessStatusCode)
                {
                    var readTask = result.Content.ReadAsAsync<IList<UserViewModel>>();
                    readTask.Wait();

                    users = readTask.Result;
                }
                else //web api sent error response 
                {
                    //log response status here..

                    users = Enumerable.Empty<UserViewModel>();

                    ModelState.AddModelError(string.Empty, "Server error. Please contact administrator.");
                }
            }

            return View(users);
        }

    }
}
