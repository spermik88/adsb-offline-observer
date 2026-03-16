let items = [];
let isRunning = false;
let customTemplates = [];
let selectedTemplateId = null;

const STORAGE_KEYS = {
    templates: 'customTemplates',
    selectedTemplateId: 'selectedTemplateId'
};

document.addEventListener('DOMContentLoaded', async () => {
    await loadTemplates();
    bindEvents();
    renderTemplateList();
});

function bindEvents() {
    document.getElementById('fileInput').addEventListener('change', handleRecipientsFileChange);
    document.getElementById('templateInput').addEventListener('change', handleTemplateFileChange);
    document.getElementById('language').addEventListener('change', () => {
        selectedTemplateId = null;
        saveTemplateSelection();
        renderTemplateList();
    });

    document.getElementById('startBtn').addEventListener('click', startSending);
    document.getElementById('stopBtn').addEventListener('click', stopSending);
}

async function loadTemplates() {
    const stored = await chrome.storage.local.get([
        STORAGE_KEYS.templates,
        STORAGE_KEYS.selectedTemplateId
    ]);

    customTemplates = Array.isArray(stored[STORAGE_KEYS.templates])
        ? stored[STORAGE_KEYS.templates]
        : [];

    const storedSelectedId = stored[STORAGE_KEYS.selectedTemplateId];
    selectedTemplateId = customTemplates.some(template => template.id === storedSelectedId)
        ? storedSelectedId
        : null;
}

async function saveTemplates() {
    await chrome.storage.local.set({
        [STORAGE_KEYS.templates]: customTemplates
    });
}

async function saveTemplateSelection() {
    await chrome.storage.local.set({
        [STORAGE_KEYS.selectedTemplateId]: selectedTemplateId
    });
}

function handleRecipientsFileChange(e) {
    const file = e.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = function(loadEvent) {
        const content = loadEvent.target.result;
        parseFile(content, file.name);
    };
    reader.readAsText(file, 'UTF-8');
}

function handleTemplateFileChange(e) {
    const file = e.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = async function(loadEvent) {
        const rawContent = String(loadEvent.target.result || '').replace(/^\uFEFF/, '');
        const normalizedContent = rawContent.replace(/\r\n/g, '\n').trim();
        if (!normalizedContent) {
            log('Ошибка: загруженный текстовый файл пустой', true);
            e.target.value = '';
            return;
        }

        const lines = normalizedContent.split('\n');
        const subject = (lines[0] || '').trim();
        const message = lines.slice(1).join('\n').trim();

        if (!subject) {
            log('Ошибка: первая строка файла должна содержать тему письма', true);
            e.target.value = '';
            return;
        }

        if (!message) {
            log('Ошибка: начиная со второй строки должен быть текст письма', true);
            e.target.value = '';
            return;
        }

        const templateId = `template_${Date.now()}`;
        const existingIndex = customTemplates.findIndex(template => template.name === file.name);
        const template = {
            id: templateId,
            name: file.name,
            subject,
            content: message
        };

        if (existingIndex >= 0) {
            customTemplates.splice(existingIndex, 1, template);
        } else {
            customTemplates.push(template);
        }

        selectedTemplateId = templateId;
        await saveTemplates();
        await saveTemplateSelection();
        renderTemplateList();
        log(`Файл "${file.name}" добавлен в список шаблонов`);
        e.target.value = '';
    };
    reader.readAsText(file, 'UTF-8');
}

function renderTemplateList() {
    const list = document.getElementById('templateList');
    list.textContent = '';

    if (customTemplates.length === 0) {
        const emptyState = document.createElement('div');
        emptyState.className = 'empty-state';
        emptyState.textContent = 'Пока нет загруженных файлов.';
        list.appendChild(emptyState);
        return;
    }

    customTemplates.forEach(template => {
        const item = document.createElement('div');
        item.className = `template-item${template.id === selectedTemplateId ? ' active' : ''}`;

        const label = document.createElement('label');
        label.className = 'template-label';

        const radio = document.createElement('input');
        radio.type = 'radio';
        radio.name = 'templateChoice';
        radio.checked = template.id === selectedTemplateId;
        radio.disabled = isRunning;
        radio.addEventListener('change', async () => {
            selectedTemplateId = template.id;
            await saveTemplateSelection();
            renderTemplateList();
        });

        const name = document.createElement('span');
        name.className = 'template-name';
        name.textContent = template.name;

        label.append(radio, name);

        const deleteBtn = document.createElement('button');
        deleteBtn.type = 'button';
        deleteBtn.className = 'icon-btn';
        deleteBtn.title = 'Удалить файл';
        deleteBtn.textContent = '🗑';
        deleteBtn.disabled = isRunning;
        deleteBtn.addEventListener('click', async () => {
            customTemplates = customTemplates.filter(item => item.id !== template.id);
            if (selectedTemplateId === template.id) {
                selectedTemplateId = null;
                await saveTemplateSelection();
            }
            await saveTemplates();
            renderTemplateList();
            log(`Файл "${template.name}" удален`);
        });

        item.append(label, deleteBtn);
        list.appendChild(item);
    });
}

function parseFile(content, filename) {
    items = [];
    const normalized = content.trim().replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');

    const isJson = filename.endsWith('.json') || content.trim().startsWith('[') || content.trim().startsWith('{');

    if (isJson) {
        parseJsonFormat(normalized);
    } else {
        parseTxtFormat(normalized);
    }
}

function parseJsonFormat(content) {
    try {
        const data = JSON.parse(content);

        if (!Array.isArray(data)) {
            log('Ошибка: JSON файл должен содержать массив', true);
            document.getElementById('count').textContent = 0;
            document.getElementById('startBtn').disabled = true;
            return;
        }

        let skipped = 0;
        data.forEach((item, index) => {
            const adUrl = item.adLink || item.ad_url;
            const title = item.title || item.seller || 'Объявление';

            if (item.valid === false) {
                skipped++;
                log(`Запись ${index + 1} пропущена. ${item.validation_info || 'Email не валиден'}`);
                return;
            }

            if (item.email && title && adUrl) {
                items.push({
                    email: String(item.email).trim(),
                    title: String(title).trim(),
                    link: String(adUrl).trim(),
                    seller: item.seller || '',
                    price: item.price || '',
                    city: item.city || ''
                });
            } else {
                skipped++;
                const missing = [];
                if (!item.email) missing.push('email');
                if (!title) missing.push('title');
                if (!adUrl) missing.push('ad_url/adLink');
                log(`Запись ${index + 1} пропущена. Отсутствуют поля: ${missing.join(', ')}`);
            }
        });

        updateStats(skipped, data.length);
    } catch (e) {
        log(`Ошибка парсинга JSON: ${e.message}`, true);
        log('Подсказка: убедитесь, что файл содержит валидный JSON массив', true);
        document.getElementById('count').textContent = 0;
        document.getElementById('startBtn').disabled = true;
    }
}

function parseTxtFormat(content) {
    try {
        const lines = content.split('\n').filter(line => line.trim());

        if (lines.length === 0) {
            log('Файл пустой', true);
            document.getElementById('count').textContent = 0;
            document.getElementById('startBtn').disabled = true;
            return;
        }

        let skipped = 0;
        lines.forEach(line => {
            const parts = line.split('\t');

            if (parts.length >= 3) {
                const email = parts[0].trim();
                const title = parts[1].trim();
                const link = parts[2].trim();

                if (email && title && link) {
                    items.push({
                        email,
                        title,
                        link,
                        seller: '',
                        price: '',
                        city: ''
                    });
                } else {
                    skipped++;
                }
            } else {
                skipped++;
            }
        });

        updateStats(skipped, lines.length);
    } catch (e) {
        log(`Ошибка парсинга TXT: ${e.message}`, true);
        document.getElementById('count').textContent = 0;
        document.getElementById('startBtn').disabled = true;
    }
}

function updateStats(skipped, total) {
    document.getElementById('count').textContent = items.length;
    document.getElementById('startBtn').disabled = items.length === 0;

    if (items.length > 0) {
        log(`Файл загружен. Найдено: ${items.length}${skipped > 0 ? ` (пропущено: ${skipped})` : ''}`);
        log(`Примеры: ${items.slice(0, 3).map(i => `${i.email} - ${i.title.substring(0, 20)}...`).join('; ')}`);
    } else {
        log(`Не найдено валидных записей. Всего: ${total}, пропущено: ${skipped}`, true);
    }
}

function getSelectedLanguageData() {
    const selectedTemplate = customTemplates.find(template => template.id === selectedTemplateId);
    if (selectedTemplate) {
        return {
            subjects: [selectedTemplate.subject],
            messages: [selectedTemplate.content]
        };
    }

    const lang = document.getElementById('language').value;
    return LANGUAGES[lang];
}

async function startSending() {
    if (items.length === 0) {
        log('Нет данных для отправки', true);
        return;
    }

    isRunning = true;
    toggleButtons(true);
    log(`Запуск рассылки на ${items.length} адресов...`);

    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab.url.includes('mail.google.com')) {
        log('Ошибка: откройте вкладку Gmail', true);
        isRunning = false;
        toggleButtons(false);
        return;
    }

    chrome.tabs.sendMessage(tab.id, {
        action: 'start_queue',
        items,
        languageData: getSelectedLanguageData()
    }, () => {
        if (chrome.runtime.lastError) {
            log('Ошибка соединения: обновите страницу Gmail и перезапустите расширение.', true);
            isRunning = false;
            toggleButtons(false);
        }
    });
}

async function stopSending() {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    chrome.tabs.sendMessage(tab.id, { action: 'stop_queue' });
    log('Остановка рассылки...');
    isRunning = false;
    toggleButtons(false);
}

chrome.runtime.onMessage.addListener((request) => {
    if (request.action === 'log') {
        log(request.message, request.isError);
    }

    if (request.action === 'finished') {
        isRunning = false;
        toggleButtons(false);
        log('Рассылка завершена');
    }
});

function log(msg, isError = false) {
    const status = document.getElementById('status');
    const line = document.createElement('div');
    line.textContent = `[${new Date().toLocaleTimeString()}] ${msg}`;
    if (isError) line.classList.add('error');
    status.prepend(line);

    if (status.children.length > 100) {
        status.removeChild(status.lastChild);
    }
}

function toggleButtons(running) {
    document.getElementById('startBtn').style.display = running ? 'none' : 'inline-block';
    document.getElementById('stopBtn').style.display = running ? 'inline-block' : 'none';
    document.getElementById('fileInput').disabled = running;
    document.getElementById('templateInput').disabled = running;
    document.getElementById('language').disabled = running;
    renderTemplateList();
}
