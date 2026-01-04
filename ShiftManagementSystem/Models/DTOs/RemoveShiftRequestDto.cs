namespace ShiftManagementSystem.Models.DTOs
{
	public class RemoveShiftRequestDTO
	{
		public string ShiftDate { get; set; }

		// 必須新增此欄位，老闆刪除他人排班時需要傳入
		public int? TargetUserId { get; set; }
	}
}
