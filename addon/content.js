let isProcessRunning = false;

// Вспомогательная функция задержки
const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));
const randomSleep = (min, max) => sleep(Math.random() * (max - min) + min);

// Селекторы (на основе ваших данных)
const SELECTORS = {
  composeBtn: 'div.T-I.T-I-KE.L3[role="button"][gh="cm"]',
  toField: 'input[peoplekit-id], input.agP.aFw', // Используем оба варианта для надежности
  subjectField: 'input[name="subjectbox"]',
  bodyField: 'div[aria-label="Текст письма"], div[role="textbox"][contenteditable="true"]',
  sendBtn: 'div.T-I.J-J5-Ji.aoO.v7.T-I-atl.L3, div[role="button"][data-tooltip*="Отправить"]'
};

function waitForElement(selector, timeout = 10000) {
  return new Promise((resolve, reject) => {
    const interval = setInterval(() => {
      const el = document.querySelector(selector);
      if (el) {
        clearInterval(interval);
        resolve(el);
      }
    }, 500);
    
    setTimeout(() => {
      clearInterval(interval);
      reject(`Элемент не найден: ${selector}`);
    }, timeout);
  });
}

async function sendEmail(item, langData) {
  try {
    // 1. Нажимаем "Написать"
    const composeBtn = document.querySelector(SELECTORS.composeBtn);
    if (!composeBtn) throw new Error("Не найдена кнопка 'Написать'");
    composeBtn.click();
    
    // Ждем появления окна (поля "Кому")
    await sleep(200); 
    const toInput = await waitForElement(SELECTORS.toField);
    
    // 2. Заполняем получателя
    toInput.click();
    toInput.value = item.email;
    toInput.dispatchEvent(new Event('input', { bubbles: true }));
    await sleep(50);
    // Имитация Enter чтобы email "зафиксировался"
    toInput.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await sleep(50);

    // 3. Тема
    const subjectInput = document.querySelector(SELECTORS.subjectField);
    const subject = langData.subjects[Math.floor(Math.random() * langData.subjects.length)];
    
    subjectInput.click();
    subjectInput.value = subject;
    subjectInput.dispatchEvent(new Event('input', { bubbles: true }));
    await sleep(100);

    // 4. Текст письма
    const bodyInput = document.querySelector(SELECTORS.bodyField);
    const msgTemplate = langData.messages[Math.floor(Math.random() * langData.messages.length)];
    // Заменяем плейсхолдеры
    const message = msgTemplate
      .replace('{title}', item.title)
      .replace('{link}', item.link)
      .replace(/\n/g, '<br>'); // Заменяем переносы строк на HTML теги

    bodyInput.click();
    bodyInput.innerHTML = message; // Для div contenteditable лучше использовать innerHTML
    bodyInput.dispatchEvent(new Event('input', { bubbles: true }));
    await sleep(1500);

    // 5. Отправка
    const sendBtn = document.querySelector(SELECTORS.sendBtn);
    if (!sendBtn) throw new Error("Не найдена кнопка отправки");
    sendBtn.click();

    // Ждем подтверждения отправки или просто небольшую паузу
    await sleep(2000);
    
    chrome.runtime.sendMessage({ action: "log", message: `✓ Отправлено: ${item.email}` });
    return true;

  } catch (err) {
    console.error(err);
    chrome.runtime.sendMessage({ action: "log", message: `Ошибка (${item.email}): ${err.message}`, isError: true });
    
    // Пытаемся закрыть окно, если что-то пошло не так (ищем кнопку корзины или закрытия)
    try {
        const discardBtn = document.querySelector('div[aria-label="Удалить черновик"]');
        if(discardBtn) discardBtn.click();
    } catch(e) {}
    
    return false;
  }
}

async function processQueue(items, langData) {
  isProcessRunning = true;
  
  for (let i = 0; i < items.length; i++) {
    if (!isProcessRunning) break;
    
    chrome.runtime.sendMessage({ action: "log", message: `[${i+1}/${items.length}] Обработка: ${items[i].email}` });
    
    await sendEmail(items[i], langData);
    
    // Случайная задержка между письмами (как в Python скрипте 1-2 сек + запас)
    const delay = Math.floor(Math.random() * 2000) + 2000; 
    await sleep(delay);
  }
  
  isProcessRunning = false;
  chrome.runtime.sendMessage({ action: "finished" });
}

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === "start_queue") {
    processQueue(request.items, request.languageData);
    sendResponse({ status: "started" });
  }
  if (request.action === "stop_queue") {
    isProcessRunning = false;
    sendResponse({ status: "stopped" });
  }
});

