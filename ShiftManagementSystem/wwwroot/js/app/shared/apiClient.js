// shared/apiClient.js

async function handleResponse(res) {
    if (!res.ok) {
        let errorData;
        try {
            // 嘗試解析後端回傳的 JSON (例如: { message: "班次已滿" })
            errorData = await res.json();
        } catch (e) {
            // 如果後端回傳的不是 JSON (例如 500 html 頁面)
            errorData = { message: await res.text() };
        }

        // 建立一個 Error 物件，並把後端資料掛載上去，模仿 Axios 結構
        const error = new Error(errorData.message || `API Error: ${res.status}`);
        error.response = {
            status: res.status,
            data: errorData
        };
        throw error;
    }
    return await res.json();
}

export async function apiGet(url) {
    const res = await fetch(url, { credentials: 'include' });
    return await handleResponse(res);
}

export async function apiPost(url, body) {
    const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(body)
    });
    return await handleResponse(res);
}

export async function apiDelete(url, body) {
    const res = await fetch(url, {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(body)
    });
    return await handleResponse(res);
}