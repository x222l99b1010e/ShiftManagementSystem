using System.ComponentModel.DataAnnotations;

namespace ShiftManagementSystem.Models.ViewModels
{
	public class LoginViewModel
	{
		[Required(ErrorMessage = "請務必輸入帳號")]
		[Display(Name = "員工帳號")]
		public string Username { get; set; }

		[Required(ErrorMessage = "請輸入密碼")]
		[DataType(DataType.Password)]
		public string Password { get; set; }

		// 可以加一個屬性來記錄「記住我」，這是 DTO 通常不會有的
		public bool RememberMe { get; set; }
	}
}
