namespace ShiftManagementSystem.Models.DTOs
{
	public class AddShiftRequestDTO
	{
		public string ShiftDate { get; set; } // 格式: "2026-02-15"

		// 新增此欄位：老闆排班時傳入員工 ID，員工排班時傳 null
		public int? TargetUserId { get; set; }
	}

}
