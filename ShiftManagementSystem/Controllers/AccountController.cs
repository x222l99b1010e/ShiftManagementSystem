using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftManagementSystem.Models;
using ShiftManagementSystem.Models.ViewModels;
using System.Security.Claims;

namespace ShiftManagementSystem.Controllers
{
	[AllowAnonymous]
	public class AccountController : Controller
	{
		private readonly ScheduleDBContext _context;
		private readonly IPasswordHasher<string> _passwordHasher;

		public AccountController(ScheduleDBContext context, IPasswordHasher<string> passwordHasher)
		{
			_context = context;
			_passwordHasher = passwordHasher;
		}

		[HttpGet]
		public IActionResult Login() => View(new LoginViewModel());

		[HttpPost]
		public async Task<IActionResult> Login(LoginViewModel model)
		{
			if (!ModelState.IsValid) return View(model);

			// 從資料庫找人
			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Username == model.Username && u.IsActive == true);

			if (user != null)
			{
				// 比對密碼 (Hash 比對)
				var verifyResult = _passwordHasher.VerifyHashedPassword(model.Username, user.PasswordHash, model.Password);

				if (verifyResult == PasswordVerificationResult.Success)
				{
					// 這裡就是原本 ClaimsTransformer 做的事：把資訊塞進 Cookie
					var claims = new List<Claim>
				{
					new Claim(ClaimTypes.Name, user.Username),
					new Claim(ClaimTypes.Role, user.Role), // Manager 或 Employee
                    new Claim("DbUserId", user.UserId.ToString()),
					new Claim("FullName", user.FullName)
				};

					var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
					var principal = new ClaimsPrincipal(identity);

					await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

					return RedirectToAction("Index", "Home");
				}
			}

			ModelState.AddModelError("", "帳號或密碼錯誤");
			return View(model);
		}

		[HttpPost] // 移除 "logout" 路由字串，讓它遵循 MVC 預設路由
		[ValidateAntiForgeryToken] // 增加 CSRF 保護，因為是從 Form Post 過來的
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return RedirectToAction("Login", "Account");
		}
	}
}
