import { apiGet, apiPost, apiDelete } from '../shared/apiClient.js';

const { createApp } = Vue;

createApp({
    data() {
        const now = new Date();
        return {
            year: now.getFullYear(),
            month: now.getMonth() + 1,
            // --- 資料容器 ---
            leaderboard: [],
            daySchedules: [],
            yearlyStats: [], // <--- [新增] 用來放年度資料

            isLoading: false,
            error: null
        };
    },
    async mounted() {
        await this.load();
    },
    methods: {
        async load() {
            this.isLoading = true;
            this.error = null;
            try {
                // 1. 載入排行榜
                const lbRes = await apiGet(`/api/statistics/leaderboard/${this.year}/${this.month}`);

                // 資料兼容性檢查：若後端回傳直接是陣列則使用，否則嘗試讀取 data 屬性
                this.leaderboard = Array.isArray(lbRes) ? lbRes : (lbRes.data || []);

                // 2. 載入月班表
                const schedRes = await apiGet(`/api/statistics/monthly-schedule/${this.year}/${this.month}`);

                // 資料結構適配：根據後端 Controller 回傳格式 (DTO) 提取 daySchedules
                const scheduleData = schedRes.daySchedules ? schedRes : (schedRes.data || {});
                this.daySchedules = scheduleData.daySchedules || [];

                // 3. [新增] 載入年度統計
                // 對應後端 API: [HttpGet("yearly-stats/{year}")] (來源: source 183)
                const yearRes = await apiGet(`/api/statistics/yearly-stats/${this.year}`);
                // 後端回傳結構是 { success: true, data: [...], message: ... }
                this.yearlyStats = yearRes.data || [];

            } catch (e) {
                // 統一錯誤訊息處理
                this.error = "載入失敗: " + (e.message || "未知錯誤");
                console.error('Error loading data:', e);
            } finally {
                this.isLoading = false;
            }
        },

        async addShiftForEmployee(date, userId) {
            try {
                const res = await apiPost('/api/shift/add', {
                    shiftDate: date,
                    targetUserId: userId
                });

                // 成功提示：使用 SweetAlert2 取代原生 alert
                Swal.fire({
                    icon: 'success',
                    title: '排班成功',
                    text: res.message || '已成功為員工排班',
                    timer: 1500,
                    showConfirmButton: false
                });

                await this.load(); // 重新整理資料
            } catch (e) {
                // 錯誤處理：解析後端回傳的 message (包含 400 滿額, 403 權限不足)
                const msg = (e.response && e.response.data && e.response.data.message)
                    ? e.response.data.message
                    : e.message;

                Swal.fire({
                    icon: 'error',
                    title: '排班失敗',
                    text: msg
                });
            }
        },

        async removeShiftForEmployee(date, userId) {
            // 確認對話框：使用 SweetAlert2 取代原生 confirm
            const confirmResult = await Swal.fire({
                title: '確定刪除排班？',
                text: `日期：${date.substring(0, 10)}`,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#d33',
                cancelButtonColor: '#3085d6',
                confirmButtonText: '是的，刪除',
                cancelButtonText: '取消'
            });

            // 若用戶點擊取消，則直接返回
            if (!confirmResult.isConfirmed) return;

            try {
                // 注意：Delete 請求的 body 傳遞方式需與 apiClient.js 定義一致
                const res = await apiDelete('/api/shift/remove', {
                    shiftDate: date,
                    targetUserId: userId
                });

                Swal.fire({
                    icon: 'success',
                    title: '已刪除',
                    text: res.message || '排班記錄已移除',
                    timer: 1500,
                    showConfirmButton: false
                });

                await this.load();
            } catch (e) {
                // 錯誤處理：同樣對齊後端的 JSON 錯誤格式
                const msg = (e.response && e.response.data && e.response.data.message)
                    ? e.response.data.message
                    : e.message;

                Swal.fire({
                    icon: 'error',
                    title: '刪除失敗',
                    text: msg
                });
            }
        }
    }
}).mount('#manager-app');