using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BOG.DropZone.Models;
using System.Diagnostics;
using BOG.DropZone.Interface;

namespace BOG.DropZone.Controllers
{
	/// <summary>
	/// Home page controller for the site
	/// </summary>
	public class HomeController : Controller
	{
		private readonly IAssemblyVersion _AssemblyVersion;

		/// <summary>
		/// Instantiated via injection
		/// </summary>
		/// <param name="assemblyVersion">(injected)</param>
		public HomeController(IAssemblyVersion assemblyVersion)
		{
			_AssemblyVersion = assemblyVersion;
		}

		/// <summary>
		/// Index page view
		/// </summary>
		/// <returns></returns>
		public IActionResult Index()
		{
			return View();
		}

		/// <summary>
		/// About page view
		/// </summary>
		/// <returns></returns>
		public IActionResult About()
		{
			ViewData["Application"] = _AssemblyVersion.Name;
			ViewData["Version"] = _AssemblyVersion.Version;

			return View();
		}

		/// <summary>
		/// Contact page view
		/// </summary>
		/// <returns></returns>
		public IActionResult Contact()
		{
			ViewData["Message"] = "Your contact page.";

			return View();
		}

		/// <summary>
		/// Privacy page view
		/// </summary>
		/// <returns></returns>
		public IActionResult Privacy()
		{
			return View();
		}

		/// <summary>
		/// Error page view
		/// </summary>
		/// <returns></returns>
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}