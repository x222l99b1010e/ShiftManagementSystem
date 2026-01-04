namespace ShiftManagementSystem.Models.DTOs
{
	/// <summary>
	/// 該日排班員工信息
	/// </summary>
	public class ShiftedEmployeeDto
	{
		public int UserId { get; set; }
		public string FullName { get; set; }
		public DateTime ShiftDate { get; set; }
	}
}
