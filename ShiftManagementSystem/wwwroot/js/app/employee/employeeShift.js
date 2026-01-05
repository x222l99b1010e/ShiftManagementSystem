import { apiGet, apiPost, apiDelete } from '../shared/apiClient.js';

const { createApp } = Vue;
//import { createApp } from 'https://unpkg.com/vue@3/dist/vue.esm-browser.js';

// 日期格式化輔助函式
function fmt(y, m, d) {
    return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
}

createApp({
    data() {
        const now = new Date();
        const next = new Date(now.getFullYear(), now.getMonth() + 1, 1);

        return {
            holidayMap: new Map(),
            year: next.getFullYear(),
            month: next.getMonth() + 1,
            disabledSet: new Set(),
            selectedSet: new Set(),
            alreadyShiftedSet: new Set(), // 用於標記資料庫中已存在的排班
            isSubmitting: false,
            error: null
        };
    },
    computed: {
        count() { return this.selectedSet.size; },
        isValid() { return this.count >= 6 && this.count <= 15; },
        days() {
            const total = new Date(this.year, this.month, 0).getDate();
            const arr = [];
            for (let day = 1; day <= total; day++) {
                const date = fmt(this.year, this.month, day);
                const disabled = this.disabledSet.has(date);
                arr.push({
                    day,
                    date,
                    disabled,
                    disabledReason: this.holidayMap.get(date) || (disabled ? '週末' : ''),
                    selected: this.selectedSet.has(date),
                    isStored: this.alreadyShiftedSet.has(date) // 標記是否為已儲存日期 (UI顯示用)
                });
            }
            return arr;
        }
    },
    async mounted() {
        await this.initCalendar();
    },
    methods: {
        async initCalendar() {
            try {
                // 1. 取得日曆結構與假日資訊
                const calRes = await apiGet(`/api/shift/calendar/${this.year}/${this.month}`);
                (calRes.holidays || []).forEach(h => {
                    this.disabledSet.add(h.date);
                    this.holidayMap.set(h.date, h.name);
                });
                (calRes.weekends || []).forEach(d => this.disabledSet.add(d));

                // 2. 取得用戶目前的排班進度 (用於回填已排日期)
                const progRes = await apiGet(`/api/shift/progress/${this.year}/${this.month}`);

                if (progRes.existingDates) {
                    progRes.existingDates.forEach(d => {
                        this.alreadyShiftedSet.add(d);
                        this.selectedSet.add(d); // 預設勾選已排好的，方便用戶修改
                    });
                }
            } catch (e) {
                // 錯誤處理：優先解析後端回傳的 JSON message (如 401 Unauthorized, 500 Error)
                this.error = (e.response && e.response.data && e.response.data.message)
                    ? e.response.data.message
                    : `載入失敗: ${e.message}`;
            }
        },
        toggle(date) {
            if (this.disabledSet.has(date) || this.isSubmitting) return;

            if (this.selectedSet.has(date)) {
                this.selectedSet.delete(date);
            } else {
                if (this.selectedSet.size >= 15) {
                    // 使用 Swal 替換原生 alert
                    Swal.fire({
                        icon: 'warning',
                        title: '已達上限',
                        text: '每月最多只能排 15 天班',
                        timer: 2000,
                        showConfirmButton: false
                    });
                    return;
                }

                // Console 提示：僅供開發除錯確認
                if (this.alreadyShiftedSet.has(date)) {
                    console.log("此日期已在您的排班記錄中");
                }

                this.selectedSet.add(date);
            }
        },
        clear() {
            this.selectedSet.clear();
            this.error = null;
        },
        async submit() {
            if (!this.isValid) {
                this.error = `請選擇 6~15 天，目前已選 ${this.count} 天`;
                return;
            }

            this.isSubmitting = true;
            this.error = null; // 每次送出前清除舊錯誤

            try {
                const payload = {
                    year: this.year,
                    month: this.month,
                    shiftDates: Array.from(this.selectedSet).sort()
                };

                const res = await apiPost('/api/shift/bulk-save', payload);

                // 成功：使用 SweetAlert2 顯示成功訊息
                await Swal.fire({
                    icon: 'success',
                    title: '儲存成功',
                    text: res.message || '您的班表已更新',
                    confirmButtonText: '好的'
                });

                // 更新本地快照，確保 UI 狀態同步
                // 關鍵：將「目前選取的」同步給「已儲存的」
                // 這樣畫面上的橘色球會瞬間全部變回藍色球，代表資料已入庫
                this.alreadyShiftedSet = new Set(this.selectedSet);
                this.error = null;

            } catch (e) {
                // 錯誤處理：核心優化，解析 Controller 回傳的 BadRequest(new { message = ... })
                if (e.response && e.response.data && e.response.data.message) {
                    this.error = e.response.data.message; // 顯示具體錯誤 (如: 某日已滿額)
                } else {
                    this.error = `傳送失敗：${e.message || '請檢查網路連線'}`;
                }

                // 失敗提示：除了紅色框框外，也彈出 Swal 提醒用戶注意
                Swal.fire({
                    icon: 'error',
                    title: '儲存失敗',
                    text: this.error
                });

                // 自動滾動到最上方，確保用戶看到紅色錯誤框
                window.scrollTo({ top: 0, behavior: 'smooth' });
            } finally {
                this.isSubmitting = false;
            }
        }
    }
}).mount('#employee-app');