const API = "/api";
const MAX_FILE_SIZE = 50 * 1024 * 1024;

const state = {
    authenticated: Boolean(localStorage.getItem("eimza_username")),
    username: localStorage.getItem("eimza_username") || "Kullanıcı",
    documents: [],
    selectedFile: null,
    documentToSign: null,
    signingFile: null,
    uploadInProgress: false,
    uploadController: null,
    requestControllers: new Set(),
    sessionVersion: 0
};

const el = {
    authView: document.getElementById("authView"),
    appView: document.getElementById("appView"),
    loginTab: document.getElementById("loginTab"),
    registerTab: document.getElementById("registerTab"),
    loginPanel: document.getElementById("loginPanel"),
    registerPanel: document.getElementById("registerPanel"),
    loginForm: document.getElementById("loginForm"),
    registerForm: document.getElementById("registerForm"),
    logoutButton: document.getElementById("logoutButton"),
    profileName: document.getElementById("profileName"),
    welcomeName: document.getElementById("welcomeName"),
    avatarInitial: document.getElementById("avatarInitial"),
    fileInput: document.getElementById("fileInput"),
    openFilePicker: document.getElementById("openFilePicker"),
    dropZone: document.getElementById("dropZone"),
    selectedFile: document.getElementById("selectedFile"),
    selectedFileName: document.getElementById("selectedFileName"),
    selectedFileSize: document.getElementById("selectedFileSize"),
    clearFileButton: document.getElementById("clearFileButton"),
    uploadButton: document.getElementById("uploadButton"),
    documentsBody: document.getElementById("documentsBody"),
    documentsLoading: document.getElementById("documentsLoading"),
    emptyState: document.getElementById("emptyState"),
    noResultsState: document.getElementById("noResultsState"),
    totalCount: document.getElementById("totalCount"),
    signedCount: document.getElementById("signedCount"),
    pendingCount: document.getElementById("pendingCount"),
    searchInput: document.getElementById("searchInput"),
    statusFilter: document.getElementById("statusFilter"),
    refreshButton: document.getElementById("refreshButton"),
    signModal: document.getElementById("signModal"),
    closeSignModal: document.getElementById("closeSignModal"),
    cancelSignButton: document.getElementById("cancelSignButton"),
    signForm: document.getElementById("signForm"),
    signFileName: document.getElementById("signFileName"),
    pinInput: document.getElementById("pinInput"),
    positionSelect: document.getElementById("positionSelect"),
    toastRegion: document.getElementById("toastRegion")
};

function setAuthTab(tab) {
    const loginActive = tab === "login";
    el.loginTab.classList.toggle("active", loginActive);
    el.registerTab.classList.toggle("active", !loginActive);
    el.loginTab.setAttribute("aria-selected", String(loginActive));
    el.registerTab.setAttribute("aria-selected", String(!loginActive));
    el.loginPanel.hidden = !loginActive;
    el.registerPanel.hidden = loginActive;
    document.getElementById(loginActive ? "loginUsername" : "registerUsername").focus();
}

localStorage.removeItem("eimza_token");

function setSession(username) {
    resetUserScopedState();
    state.authenticated = true;
    state.username = username;
    localStorage.setItem("eimza_username", username);
}

function clearSession(showMessage = false) {
    resetUserScopedState();
    state.authenticated = false;
    state.username = "Kullanıcı";
    state.documents = [];
    localStorage.removeItem("eimza_token");
    localStorage.removeItem("eimza_username");
    el.appView.hidden = true;
    el.authView.hidden = false;
    closeSignModal();
    if (showMessage) toast("Oturum sona erdi", "Güvenliğiniz için yeniden giriş yapın.", "error");
}

function resetUserScopedState() {
    state.sessionVersion += 1;
    state.requestControllers.forEach(controller => controller.abort());
    state.requestControllers.clear();
    state.uploadController?.abort();
    state.uploadController = null;
    state.uploadInProgress = false;
    clearSelectedFile();
    closeSignModal();
    state.documents = [];
    el.searchInput.value = "";
    el.statusFilter.value = "all";
    el.documentsBody.replaceChildren();
    el.toastRegion.replaceChildren();
    updateMetrics();
    el.fileInput.disabled = false;
    el.clearFileButton.disabled = false;
    setButtonBusy(el.uploadButton, false);
}

async function apiFetch(path, options = {}, authenticated = true) {
    const headers = new Headers(options.headers || {});

    const requestController = authenticated ? new AbortController() : null;
    if (requestController) {
        state.requestControllers.add(requestController);
        if (options.signal?.aborted) requestController.abort();
        else options.signal?.addEventListener("abort", () => requestController.abort(), { once: true });
    }

    let response;
    try {
        response = await fetch(`${API}${path}`, {
            ...options,
            headers,
            credentials: "same-origin",
            signal: requestController?.signal || options.signal
        });
    } catch (error) {
        if (error.name === "AbortError") throw error;
        throw new Error("Sunucuya ulaşılamıyor. Sistem servislerini kontrol edin.");
    } finally {
        if (requestController) state.requestControllers.delete(requestController);
    }

    if (authenticated && response.status === 401) {
        clearSession(true);
        const error = new Error("Oturumunuzun süresi dolmuş. Lütfen yeniden giriş yapın.");
        error.name = "SessionExpiredError";
        throw error;
    }

    return response;
}

async function responseError(response, fallback) {
    try {
        const contentType = response.headers.get("content-type") || "";
        if (contentType.includes("application/json")) {
            const data = await response.json();
            return data.message || data.error || fallback;
        }
        const text = await response.text();
        if (text && text.length < 300) return text;
    } catch { /* use fallback */ }
    return fallback;
}

function toast(title, message, type = "success") {
    const node = document.createElement("div");
    node.className = `toast ${type}`;

    const icon = document.createElement("span");
    icon.className = "toast-icon";
    icon.textContent = type === "error" ? "!" : "✓";

    const copy = document.createElement("div");
    const strong = document.createElement("strong");
    const small = document.createElement("small");
    strong.textContent = title;
    small.textContent = message;
    copy.append(strong, small);
    node.append(icon, copy);
    el.toastRegion.append(node);

    window.setTimeout(() => node.remove(), 4500);
}

function setButtonBusy(button, busy, busyText) {
    if (!button.dataset.originalHtml) button.dataset.originalHtml = button.innerHTML;
    button.disabled = busy;
    button.innerHTML = busy ? `<span class="spinner" style="width:16px;height:16px;border-width:2px"></span><span>${busyText}</span>` : button.dataset.originalHtml;
}

async function handleLogin(event) {
    event.preventDefault();
    const button = el.loginForm.querySelector("button[type='submit']");
    const username = document.getElementById("loginUsername").value.trim();
    const password = document.getElementById("loginPassword").value;
    setButtonBusy(button, true, "Giriş yapılıyor");

    try {
        const response = await apiFetch("/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username, password })
        }, false);
        if (!response.ok) throw new Error(await responseError(response, "Kullanıcı adı veya şifre hatalı."));
        const data = await response.json();
        setSession(data.username || username);
        el.loginForm.reset();
        await showApp();
        toast("Hoş geldiniz", "Güvenli belge kasanız hazır.");
    } catch (error) {
        toast("Giriş yapılamadı", error.message, "error");
    } finally {
        setButtonBusy(button, false);
    }
}

async function handleRegister(event) {
    event.preventDefault();
    const button = el.registerForm.querySelector("button[type='submit']");
    const username = document.getElementById("registerUsername").value.trim();
    const email = document.getElementById("registerEmail").value.trim();
    const password = document.getElementById("registerPassword").value;
    setButtonBusy(button, true, "Hesap oluşturuluyor");

    try {
        const response = await apiFetch("/auth/register", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username, email, password })
        }, false);
        if (!response.ok) throw new Error(await responseError(response, "Hesap oluşturulamadı."));
        const data = await response.json();
        setSession(data.username || username);
        el.registerForm.reset();
        await showApp();
        toast("Hesabınız hazır", "İlk belgenizi yükleyebilirsiniz.");
    } catch (error) {
        toast("Kayıt tamamlanamadı", error.message, "error");
    } finally {
        setButtonBusy(button, false);
    }
}

async function showApp() {
    el.authView.hidden = true;
    el.appView.hidden = false;
    el.profileName.textContent = state.username;
    el.welcomeName.textContent = state.username;
    el.avatarInitial.textContent = (state.username[0] || "K").toLocaleUpperCase("tr-TR");
    await fetchDocuments();
}

function validateFile(file) {
    if (!file) return false;
    const pdf = file.type === "application/pdf" || file.name.toLocaleLowerCase("tr-TR").endsWith(".pdf");
    if (!pdf) {
        toast("Geçersiz dosya", "Yalnızca PDF belgeleri yükleyebilirsiniz.", "error");
        return false;
    }
    if (file.size > MAX_FILE_SIZE) {
        toast("Dosya çok büyük", "PDF belgesi en fazla 50 MB olabilir.", "error");
        return false;
    }
    return true;
}

function selectFile(file) {
    if (!validateFile(file)) return;
    state.selectedFile = file;
    el.selectedFileName.textContent = file.name;
    el.selectedFileSize.textContent = formatBytes(file.size);
    el.dropZone.hidden = true;
    el.selectedFile.hidden = false;
}

function clearSelectedFile() {
    state.selectedFile = null;
    el.fileInput.value = "";
    el.selectedFile.hidden = true;
    el.dropZone.hidden = false;
}

async function uploadSelectedFile() {
    if (!state.selectedFile || state.uploadInProgress) return;
    const file = state.selectedFile;
    const sessionVersion = state.sessionVersion;
    const controller = new AbortController();
    state.uploadInProgress = true;
    state.uploadController = controller;
    el.fileInput.disabled = true;
    el.clearFileButton.disabled = true;
    setButtonBusy(el.uploadButton, true, "Yükleniyor");
    const data = new FormData();
    data.append("file", file);

    try {
        const response = await apiFetch("/documents/upload", { method: "POST", body: data, signal: controller.signal });
        if (sessionVersion !== state.sessionVersion) return;
        if (!response.ok) throw new Error(await responseError(response, "Belge yüklenemedi."));
        clearSelectedFile();
        await fetchDocuments(false);
        toast("Belge kasaya eklendi", `${file.name} imzalanmaya hazır.`);
    } catch (error) {
        if (error.name === "AbortError" || error.name === "SessionExpiredError") return;
        toast("Yükleme başarısız", error.message, "error");
    } finally {
        if (state.uploadController === controller) {
            state.uploadController = null;
            state.uploadInProgress = false;
            el.fileInput.disabled = false;
            el.clearFileButton.disabled = false;
            setButtonBusy(el.uploadButton, false);
        }
    }
}

async function fetchDocuments(showLoader = true) {
    const sessionVersion = state.sessionVersion;
    if (showLoader) {
        el.documentsLoading.hidden = false;
        el.emptyState.hidden = true;
        el.noResultsState.hidden = true;
        el.documentsBody.replaceChildren();
    }

    try {
        const response = await apiFetch("/documents/my-documents");
        if (!response.ok) throw new Error(await responseError(response, "Belgeler alınamadı."));
        const documents = await response.json();
        if (sessionVersion !== state.sessionVersion || !state.authenticated) return;
        state.documents = documents;
        state.documents.sort((a, b) => new Date(b.uploadedAt) - new Date(a.uploadedAt));
        updateMetrics();
        renderDocuments();
    } catch (error) {
        if (error.name === "AbortError" || sessionVersion !== state.sessionVersion) return;
        if (state.authenticated) toast("Belgeler yüklenemedi", error.message, "error");
    } finally {
        if (sessionVersion === state.sessionVersion) el.documentsLoading.hidden = true;
    }
}

function isSigned(document) {
    return (document.status || "").toLocaleUpperCase("tr-TR").includes("İMZ") || (document.status || "").toUpperCase().includes("IMZ");
}

function updateMetrics() {
    const signed = state.documents.filter(isSigned).length;
    el.totalCount.textContent = String(state.documents.length);
    el.signedCount.textContent = String(signed);
    el.pendingCount.textContent = String(state.documents.length - signed);
}

function filteredDocuments() {
    const query = el.searchInput.value.trim().toLocaleLowerCase("tr-TR");
    const filter = el.statusFilter.value;
    return state.documents.filter(document => {
        const matchesText = (document.originalFileName || "").toLocaleLowerCase("tr-TR").includes(query);
        const signed = isSigned(document);
        const matchesStatus = filter === "all" || (filter === "signed" && signed) || (filter === "pending" && !signed);
        return matchesText && matchesStatus;
    });
}

function renderDocuments() {
    const documents = filteredDocuments();
    el.documentsBody.replaceChildren();
    el.emptyState.hidden = state.documents.length !== 0;
    el.noResultsState.hidden = state.documents.length === 0 || documents.length !== 0;

    documents.forEach(document => {
        const signed = isSigned(document);
        const row = documentRow(document, signed);
        el.documentsBody.append(row);
    });
}

function documentRow(item, signed) {
    const row = document.createElement("tr");

    const nameCell = document.createElement("td");
    const nameWrap = document.createElement("div");
    nameWrap.className = "document-name";
    const icon = document.createElement("span");
    icon.className = "document-icon";
    icon.textContent = "PDF";
    const copy = document.createElement("div");
    const name = document.createElement("strong");
    const meta = document.createElement("small");
    name.textContent = item.originalFileName || "İsimsiz belge";
    meta.textContent = `Belge No: ${String(item.id).padStart(5, "0")}`;
    copy.append(name, meta);
    nameWrap.append(icon, copy);
    nameCell.append(nameWrap);

    const statusCell = document.createElement("td");
    statusCell.dataset.label = "Durum";
    const badge = document.createElement("span");
    badge.className = `status-badge ${signed ? "status-signed" : "status-pending"}`;
    badge.textContent = signed ? "İmzalandı" : "İmza bekliyor";
    statusCell.append(badge);

    const dateCell = document.createElement("td");
    dateCell.dataset.label = "Yüklenme";
    dateCell.textContent = formatDate(item.uploadedAt);

    const actionsCell = document.createElement("td");
    actionsCell.dataset.label = "İşlemler";
    actionsCell.className = "align-right";
    const actions = document.createElement("div");
    actions.className = "row-actions";
    const download = actionButton("İndir", "download", item.id);
    actions.append(download);
    if (!signed) actions.append(actionButton("İmzala", "sign", item.id, true));
    actionsCell.append(actions);

    row.append(nameCell, statusCell, dateCell, actionsCell);
    return row;
}

function actionButton(label, action, id, primary = false) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `action-button${primary ? " sign" : ""}`;
    button.dataset.action = action;
    button.dataset.id = String(id);
    button.textContent = label;
    return button;
}

async function handleDocumentAction(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) return;
    const item = state.documents.find(document => String(document.id) === button.dataset.id);
    if (!item) return;

    if (button.dataset.action === "download") await downloadDocument(item, button);
    if (button.dataset.action === "sign") await prepareSigning(item, button);
}

async function getDocumentBlob(item) {
    const response = await apiFetch(`/documents/${item.id}/download`);
    if (!response.ok) throw new Error(await responseError(response, "Belge indirilemedi."));
    return response.blob();
}

async function downloadDocument(item, button) {
    setButtonBusy(button, true, "Hazırlanıyor");
    try {
        const blob = await getDocumentBlob(item);
        saveBlob(blob, item.originalFileName);
        toast("İndirme başladı", item.originalFileName);
    } catch (error) {
        if (error.name === "SessionExpiredError") return;
        toast("Belge indirilemedi", error.message, "error");
    } finally {
        setButtonBusy(button, false);
    }
}

async function prepareSigning(item, button) {
    setButtonBusy(button, true, "Açılıyor");
    try {
        const blob = await getDocumentBlob(item);
        state.documentToSign = item;
        state.signingFile = new File([blob], item.originalFileName, { type: "application/pdf" });
        el.signFileName.textContent = item.originalFileName;
        el.signModal.hidden = false;
        document.body.style.overflow = "hidden";
        window.setTimeout(() => el.positionSelect.focus(), 50);
    } catch (error) {
        if (error.name === "SessionExpiredError") return;
        toast("İmza işlemi açılamadı", error.message, "error");
    } finally {
        setButtonBusy(button, false);
    }
}

function closeSignModal() {
    el.signModal.hidden = true;
    document.body.style.overflow = "";
    el.signForm.reset();
    state.documentToSign = null;
    state.signingFile = null;
}

async function handleSign(event) {
    event.preventDefault();
    if (!state.documentToSign || !state.signingFile) return;
    const button = el.signForm.querySelector("button[type='submit']");
    const item = state.documentToSign;
    const data = new FormData();
    data.append("file", state.signingFile);
    data.append("pin", el.pinInput.value);
    data.append("signaturePosition", el.positionSelect.value);
    setButtonBusy(button, true, "İmzalanıyor");

    try {
        const response = await apiFetch("/documents/sign-with-service", { method: "POST", body: data });
        if (!response.ok) throw new Error(await responseError(response, "E-imza işlemi tamamlanamadı."));
        const signedBlob = await response.blob();

        const archiveData = new FormData();
        archiveData.append("file", new File([signedBlob], `signed_${item.originalFileName}`, { type: "application/pdf" }));
        const archiveResponse = await apiFetch(`/documents/${item.id}/upload-signed`, { method: "POST", body: archiveData });

        saveBlob(signedBlob, `signed_${item.originalFileName}`);
        closeSignModal();
        await fetchDocuments(false);

        if (archiveResponse.ok) {
            toast("Belge başarıyla imzalandı", "İmzalı kopya kasaya kaydedildi ve indirildi.");
        } else {
            toast("İmza tamamlandı", "Dosya indirildi fakat arşiv durumu güncellenemedi.", "error");
        }
    } catch (error) {
        if (error.name === "SessionExpiredError") return;
        toast("İmzalama başarısız", error.message, "error");
    } finally {
        el.pinInput.value = "";
        setButtonBusy(button, false);
    }
}

function saveBlob(blob, filename) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = filename;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function formatBytes(bytes) {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(value) {
    if (!value) return "—";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "—";
    return new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }).format(date);
}

el.loginTab.addEventListener("click", () => setAuthTab("login"));
el.registerTab.addEventListener("click", () => setAuthTab("register"));
el.loginForm.addEventListener("submit", handleLogin);
el.registerForm.addEventListener("submit", handleRegister);
el.logoutButton.addEventListener("click", async () => {
    try { await apiFetch("/auth/logout", { method: "POST" }, false); }
    finally { clearSession(false); }
});
el.openFilePicker.addEventListener("click", () => el.fileInput.click());
el.dropZone.addEventListener("click", () => el.fileInput.click());
el.dropZone.addEventListener("keydown", event => {
    if (event.key === "Enter" || event.key === " ") { event.preventDefault(); el.fileInput.click(); }
});
el.fileInput.addEventListener("change", () => selectFile(el.fileInput.files[0]));
el.clearFileButton.addEventListener("click", clearSelectedFile);
el.uploadButton.addEventListener("click", uploadSelectedFile);
el.refreshButton.addEventListener("click", () => fetchDocuments());
el.searchInput.addEventListener("input", renderDocuments);
el.statusFilter.addEventListener("change", renderDocuments);
el.documentsBody.addEventListener("click", handleDocumentAction);
el.closeSignModal.addEventListener("click", closeSignModal);
el.cancelSignButton.addEventListener("click", closeSignModal);
el.signForm.addEventListener("submit", handleSign);
el.signModal.addEventListener("click", event => { if (event.target === el.signModal) closeSignModal(); });
document.addEventListener("keydown", event => { if (event.key === "Escape" && !el.signModal.hidden) closeSignModal(); });

["dragenter", "dragover"].forEach(type => el.dropZone.addEventListener(type, event => {
    event.preventDefault();
    el.dropZone.classList.add("dragging");
}));
["dragleave", "drop"].forEach(type => el.dropZone.addEventListener(type, event => {
    event.preventDefault();
    el.dropZone.classList.remove("dragging");
}));
el.dropZone.addEventListener("drop", event => selectFile(event.dataTransfer.files[0]));

if (state.authenticated) showApp();
