using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShiftManagementSystem.Controllers
{
	[Authorize(Roles = "Employee,Manager")]
	//[Authorize]
	public class ShiftPageController : Controller
	{
		public IActionResult Index() => View();
	}
}
