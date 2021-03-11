using System;
using System.Diagnostics;
using BOG.DropZone.Interface;
using BOG.DropZone.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BOG.SwissArmyKnife.Extensions;
using System.Text;

namespace BOG.DropZone.Controllers
{
	/// <summary>
	/// Home page controller for the site
	/// </summary>
	public class HomeController : Controller
	{
		private readonly IAssemblyVersion _AssemblyVersion;
		private readonly IStorage _Storage;

		/// <summary>
		/// Instantiated via injection
		/// </summary>
		/// <param name="assemblyVersion">(injected)</param>
		/// <param name="storage">(injected)</param>
		public HomeController(IAssemblyVersion assemblyVersion, IStorage storage)
		{
			_AssemblyVersion = assemblyVersion;
			_Storage = storage;
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
		/// Content page view
		/// </summary>
		/// <returns></returns>
		public IActionResult Content()
		{
			//if (Request.Form.Count > 0)
			//{
			//	ViewData["AdminToken"] = ((string)Request.Form["admin"]);
			//	SetCookieValue("AdminToken", ((string)Request.Form["admin"]).Base64EncodeString(), DateTime.Now.AddDays(1));
			//	ViewData["AccessToken"] = ((string)Request.Form["access"]);
			//	SetCookieValue("AccessToken", ((string)Request.Form["access"]).Base64EncodeString(), DateTime.Now.AddDays(1));
			//}
			//else
			//{
			//	ViewData["AdminToken"] = GetCookieValue("AdminToken", "YourAdminTokenValueHere".Base64EncodeString()).Base64DecodeString();
			//	ViewData["AccessToken"] = GetCookieValue("AccessToken", "YourAccessTokenValueHere".Base64EncodeString()).Base64DecodeString();
			//}
			var Zones = new StringBuilder();
			Zones.AppendLine("<table>");
			foreach (var key in _Storage.DropZoneList.Keys)
			{
				Zones.AppendLine("<tr>");
				Zones.AppendLine(string.Format("<td>{0}</td>", key));
				Zones.AppendLine(string.Format("<td>{0}</td>", _Storage.DropZoneList[key].Payloads.Count));
				Zones.AppendLine(string.Format("<td>{0}</td>", _Storage.DropZoneList[key].References.Count));
				Zones.AppendLine(string.Format("<td>{0:s}</td>", _Storage.DropZoneList[key].Statistics.LastDropoff));
				Zones.AppendLine(string.Format("<td>{0:s}</td>", _Storage.DropZoneList[key].Statistics.LastPickup));
				Zones.AppendLine("</tr>");
			}
			Zones.AppendLine("</table>");
			ViewData["ZoneList"] = Zones.ToString();
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

		private string GetCookieValue(string key, string defaultValue)
		{
			return HttpContext.Request.Cookies.ContainsKey(key) ? HttpContext.Request.Cookies[key] : defaultValue;
		}

		private void SetCookieValue(string key, string value, DateTime expiration)
		{
			CookieOptions option = new CookieOptions();
			option.Expires = expiration;
			if (HttpContext.Request.Cookies.ContainsKey(key))
			{
				HttpContext.Response.Cookies.Delete(key);
			}
			HttpContext.Response.Cookies.Append(key, value, option);
		}
	}
}