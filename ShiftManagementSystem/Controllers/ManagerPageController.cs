using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShiftManagementSystem.Controllers
{
	[Authorize(Roles = "Manager")]
	//[Authorize]
	public class ManagerPageController : Controller
	{
		public IActionResult Index() => View();
	}
}
