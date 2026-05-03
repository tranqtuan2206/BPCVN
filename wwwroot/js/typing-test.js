/**
 * ============================================================================
 * BPCVN — Mini Typing Test (Trang độc lập)
 * ============================================================================
 * Vanilla JS thuần — KHÔNG dùng jQuery.
 * Sử dụng Chart.js (CDN) để vẽ biểu đồ WPM theo thời gian.
 * Màu sắc Chart.js đồng bộ với brand (#fb7aec — hồng/tím).
 *
 * ── FLOW LOGIC TỔNG QUAN ──
 * 1. Random từ tiếng Việt 100% thuần → render mỗi ký tự = 1 <span>.
 * 2. Lắng nghe keydown trên document (KHÔNG dùng textarea/input).
 * 3. Khi gõ: so sánh ký tự, đánh dấu correct/incorrect, di chuyển caret.
 * 4. Mỗi giây ghi log WPM vào mảng wpmHistory[] để vẽ biểu đồ.
 * 5. Hết từ → ẩn typing phase, hiện result phase + vẽ Chart.js.
 *
 * ── CÔNG THỨC WPM ──
 * WPM = (correctChars / 5) / elapsedMinutes
 * Trong đó: 5 là số ký tự trung bình 1 từ (chuẩn quốc tế).
 *
 * Raw WPM = (totalChars / 5) / elapsedMinutes
 * Accuracy = (correctChars / totalChars) * 100
 * ============================================================================
 */

(function () {
    'use strict';

    // ═══════════════════════════════════════════════════════════════════════
    // MẢNG TỪ VỰNG TIẾNG VIỆT THUẦN (100% tiếng Việt, không lẫn tiếng Anh)
    // Bao gồm từ phổ biến + từ liên quan đến phím cơ
    // ═══════════════════════════════════════════════════════════════════════
    const WORDS = [
        // ── Từ phổ biến hàng ngày ──
        'và','của','có','trong','không','một','là','cho','các','này',
        'được','với','từ','người','đã','những','tôi','năm','về','như',
        'khi','bạn','ra','đến','hay','lại','còn','phải','nếu','thì',
        'cũng','rất','sau','mà','nhiều','việc','nên','vì','sẽ','đang',
        'tại','trên','theo','mới','qua','giữa','đây','thế','vào','hơn',
        'cần','nhất','chỉ','trước','rồi','hoặc','nào','đó','bên','học',
        'làm','biết','nói','đi','xem','thấy','lên','gì','sao','ai',
        'mình','ấy','lúc','ngày','đều','bao','giờ','luôn','tốt','cao',
        'dài','lớn','nhỏ','đẹp','mạnh','nhanh','chậm','sáng','tối','trời',
        'mây','nước','gió','mưa','nắng',
        // ── Từ liên quan đến phím cơ ──
        'phím','bàn','gõ','âm','thanh','thử','tốc','độ','chữ','trục',
        'mạch','êm','ái','lạch','cạch','dây','cáp','đèn','nắp','vỏ',
        'đệm','cao','su','nhôm','đồng','thép','nhựa','gắn','lắp','ráp',
        'hàn','chỉnh','sửa','bôi','mỡ','kéo','đẩy','nảy','bật','tắt',
        'bấm','nhấn','giữ','thả','rung','vang','trầm','bổng','ồn','yên',
        // ── Từ trải nghiệm / cộng đồng ──
        'cộng','đồng','chia','sẻ','trải','nghiệm','đánh','giá','yêu',
        'thích','chất','lượng','thiết','kế','màu','sắc','vật','liệu',
        'công','nghệ','máy','tính','phần','mềm','cứng','phát','triển',
        'viết','đọc','nghe','nhìn','chơi','chạy','nhập','xuất','điểm',
        'số','bảng','xếp','hạng','mỗi','lần','tên','đỉnh','hoàn','thành',
        'chính','xác','kết','quả','thời','gian','phút','giây','bắt','đầu',
        'tiếp','tục','dừng','chờ','sẵn','sàng','tuyệt','vời','xuất','giỏi',
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // CẤU HÌNH — MÀU SẮC ĐỒNG BỘ VỚI BRAND
    // ═══════════════════════════════════════════════════════════════════════
    const BRAND_PINK = '#fb7aec';
    const BRAND_PINK_ALPHA = 'rgba(251, 122, 236, 0.15)';

    const WORD_COUNTS = [10, 25, 50];
    let selectedWordCount = 25;

    // ═══════════════════════════════════════════════════════════════════════
    // BIẾN TRẠNG THÁI
    // ═══════════════════════════════════════════════════════════════════════
    let words = [];              // Mảng các từ đã random
    let wordIndex = 0;           // Chỉ số từ hiện tại
    let charIndex = 0;           // Chỉ số ký tự trong từ hiện tại
    let correctChars = 0;        // Tổng ký tự gõ đúng
    let incorrectChars = 0;      // Tổng ký tự gõ sai
    let totalChars = 0;          // Tổng ký tự đã gõ (đúng + sai + extra)
    let isStarted = false;       // Đã bắt đầu gõ?
    let isFinished = false;      // Đã kết thúc?
    let startTime = null;        // Timestamp bắt đầu
    let timerInterval = null;    // setInterval ID
    let elapsedSeconds = 0;      // Số giây đã trôi qua

    /**
     * Mảng ghi log WPM tại mỗi giây — dùng cho Chart.js.
     * wpmHistory[i] = WPM tại giây thứ i+1.
     */
    let wpmHistory = [];
    let rawWpmHistory = [];

    // Biến timeout để phát hiện idle → bật lại caret blink
    let typingTimeout = null;

    // ═══════════════════════════════════════════════════════════════════════
    // THAM CHIẾU DOM
    // ═══════════════════════════════════════════════════════════════════════
    let wrapper, wordsEl, caretEl, liveStats, liveWpm;
    let typingPhase, resultPhase;
    let chartCanvas, chartInstance;

    // ═══════════════════════════════════════════════════════════════════════
    // KHỞI TẠO
    // ═══════════════════════════════════════════════════════════════════════
    function init() {
        wrapper = document.getElementById('mtWrapper');
        if (!wrapper) return;

        wordsEl = document.getElementById('mtWords');
        caretEl = document.getElementById('mtCaret');
        liveStats = document.getElementById('mtLiveStats');
        liveWpm = document.getElementById('mtLiveWpm');
        typingPhase = document.getElementById('mtTypingPhase');
        resultPhase = document.getElementById('mtResultPhase');
        chartCanvas = document.getElementById('mtChart');

        // Gắn sự kiện toolbar (chọn số từ)
        document.querySelectorAll('.mt-toolbar-btn[data-count]').forEach(btn => {
            btn.addEventListener('click', () => {
                selectedWordCount = parseInt(btn.dataset.count);
                document.querySelectorAll('.mt-toolbar-btn[data-count]').forEach(b =>
                    b.classList.remove('active'));
                btn.classList.add('active');
                resetTest();
            });
        });

        // Gắn sự kiện restart
        document.querySelectorAll('.mt-restart-trigger').forEach(btn => {
            btn.addEventListener('click', resetTest);
        });

        // Click vào vùng text để focus
        wordsEl.addEventListener('click', () => {
            if (!isFinished) {
                wordsEl.classList.remove('blur');
                wordsEl.focus();
            }
        });

        /**
         * ── SỰ KIỆN GÕ PHÍM ────────────────────────────────────────────
         * Lắng nghe keydown trên document.
         * KHÔNG dùng textarea hay input — chỉ keydown thuần.
         */
        document.addEventListener('keydown', handleKeyDown);

        // Blur text khi click ra ngoài
        document.addEventListener('click', (e) => {
            if (!wrapper.contains(e.target) && !isFinished && isStarted) {
                wordsEl.classList.add('blur');
            }
        });

        resetTest();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RESET TEST — random từ mới, xóa trạng thái cũ
    // ═══════════════════════════════════════════════════════════════════════
    function resetTest() {
        if (timerInterval) { clearInterval(timerInterval); timerInterval = null; }

        wordIndex = 0;
        charIndex = 0;
        correctChars = 0;
        incorrectChars = 0;
        totalChars = 0;
        isStarted = false;
        isFinished = false;
        startTime = null;
        elapsedSeconds = 0;
        wpmHistory = [];
        rawWpmHistory = [];

        words = generateWords(selectedWordCount);

        typingPhase.style.display = '';
        resultPhase.classList.remove('show');
        liveStats.classList.remove('visible');
        if (caretEl) caretEl.style.display = '';

        if (chartInstance) { chartInstance.destroy(); chartInstance = null; }

        renderWords();
        updateCaret();
        wordsEl.classList.remove('blur');
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SINH TỪ NGẪU NHIÊN — chỉ lấy từ tiếng Việt thuần từ mảng WORDS
    // ═══════════════════════════════════════════════════════════════════════
    function generateWords(count) {
        const result = [];
        for (let i = 0; i < count; i++) {
            result.push(WORDS[Math.floor(Math.random() * WORDS.length)]);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDER TỪ: Mỗi ký tự = 1 <span class="mt-char">
    // ═══════════════════════════════════════════════════════════════════════
    function renderWords() {
        // Xóa chỉ các .mt-word cũ, GIỮ LẠI caret (vì caret nằm trong mtWords)
        wordsEl.querySelectorAll('.mt-word').forEach(w => w.remove());

        words.forEach((word, wi) => {
            const wordDiv = document.createElement('div');
            wordDiv.className = 'mt-word';
            wordDiv.dataset.index = wi;

            for (let ci = 0; ci < word.length; ci++) {
                const span = document.createElement('span');
                span.className = 'mt-char';
                span.textContent = word[ci];
                span.dataset.ci = ci;
                wordDiv.appendChild(span);
            }
            // Chèn TRƯỚC caret (caret luôn là phần tử cuối)
            wordsEl.insertBefore(wordDiv, caretEl);
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CẬP NHẬT VỊ TRÍ CARET (con trỏ nhấp nháy)
    // ═══════════════════════════════════════════════════════════════════════
    /**
     * Caret là thẻ <div> absolute-positioned.
     * Dùng getBoundingClientRect() của span hiện tại
     * để tính toạ độ left/top tương đối với .mt-words.
     */
    function updateCaret() {
        if (!caretEl || isFinished) return;

        const wordDivs = wordsEl.querySelectorAll('.mt-word');
        if (wordIndex >= wordDivs.length) return;

        const currentWordDiv = wordDivs[wordIndex];
        const chars = currentWordDiv.querySelectorAll('.mt-char');

        let targetEl;
        if (charIndex < chars.length) {
            targetEl = chars[charIndex];
        } else if (chars.length > 0) {
            targetEl = chars[chars.length - 1];
        }

        if (!targetEl) return;

        const wordsRect = wordsEl.getBoundingClientRect();
        const charRect = targetEl.getBoundingClientRect();

        let left;
        if (charIndex >= chars.length && chars.length > 0) {
            left = charRect.right - wordsRect.left;
        } else {
            left = charRect.left - wordsRect.left;
        }
        const top = charRect.top - wordsRect.top;

        caretEl.style.left = left + 'px';
        caretEl.style.top = top + 'px';
        caretEl.style.height = charRect.height + 'px';

        // Scroll: nếu caret xuống dòng 3+ → cuộn container lên
        if (top > 72) {
            const lineH = charRect.height * 2;
            wordsEl.scrollTop += lineH;
            requestAnimationFrame(updateCaret);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // XỬ LÝ PHÍM GÕ (keydown)
    // ═══════════════════════════════════════════════════════════════════════
    /**
     * ── THUẬT TOÁN ĐỐI CHIẾU KÝ TỰ ──
     * 1. Lấy từ hiện tại (words[wordIndex]) và ký tự tại charIndex.
     * 2. Space → chuyển sang từ tiếp theo.
     * 3. Backspace → quay lại ký tự trước.
     * 4. Ký tự thường → so sánh:
     *    - Khớp  → span class 'correct', correctChars++
     *    - Sai   → span class 'incorrect', incorrectChars++
     * 5. Tăng charIndex, cập nhật caret.
     */
    function handleKeyDown(e) {
        if (isFinished) return;
        const tag = document.activeElement?.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;

        const key = e.key;

        // Bỏ qua modifier keys và phím chức năng (ngoại trừ Ctrl + Backspace để xóa nhanh)
        if ((e.ctrlKey && key !== 'Backspace') || e.altKey || e.metaKey) return;
        if (['Shift','CapsLock','Tab','Escape','Enter',
             'ArrowUp','ArrowDown','ArrowLeft','ArrowRight',
             'Home','End','PageUp','PageDown','Insert','Delete',
             'F1','F2','F3','F4','F5','F6','F7','F8','F9','F10','F11','F12'
        ].includes(key)) return;

        e.preventDefault();

        // Bắt đầu đếm giờ khi gõ phím đầu tiên
        if (!isStarted) {
            isStarted = true;
            startTime = Date.now();
            startTimer();
            liveStats.classList.add('visible');
            wordsEl.classList.remove('blur');
        }

        // Đánh dấu đang gõ (tắt blink caret)
        caretEl.classList.add('typing');
        if (typingTimeout) clearTimeout(typingTimeout);
        typingTimeout = setTimeout(() => caretEl.classList.remove('typing'), 500);

        const currentWord = words[wordIndex];
        const wordDivs = wordsEl.querySelectorAll('.mt-word');
        const currentWordDiv = wordDivs[wordIndex];
        const chars = currentWordDiv.querySelectorAll('.mt-char');

        // ── SPACE: chuyển sang từ tiếp theo ─────────────────────────────
        if (key === ' ') {
            if (charIndex === 0) return;

            // Đánh dấu ký tự chưa gõ là incorrect
            for (let i = charIndex; i < currentWord.length; i++) {
                if (!chars[i].classList.contains('correct') &&
                    !chars[i].classList.contains('incorrect')) {
                    chars[i].classList.add('incorrect');
                    incorrectChars++;
                    totalChars++;
                }
            }

            wordIndex++;
            charIndex = 0;

            if (wordIndex >= words.length) {
                finishTest();
                return;
            }

            updateCaret();
            return;
        }

        // ── BACKSPACE: quay lại ký tự trước hoặc xóa cả từ (Ctrl + Backspace) ──
        if (key === 'Backspace') {
            const deleteChar = () => {
                if (charIndex > 0) {
                    charIndex--;
                    const prevChar = chars[charIndex];

                    if (prevChar && prevChar.classList.contains('extra')) {
                        if (prevChar.classList.contains('incorrect')) incorrectChars--;
                        totalChars--;
                        prevChar.remove();
                    } else if (prevChar) {
                        if (prevChar.classList.contains('correct')) correctChars--;
                        if (prevChar.classList.contains('incorrect')) incorrectChars--;
                        totalChars--;
                        prevChar.classList.remove('correct', 'incorrect');
                    }
                }
            };

            if (e.ctrlKey) {
                // Ctrl + Backspace: Xóa toàn bộ ký tự đã gõ của từ hiện tại
                while (charIndex > 0) {
                    deleteChar();
                }
            } else {
                // Backspace bình thường: Xóa 1 ký tự
                deleteChar();
            }

            updateCaret();
            return;
        }

        // ── KÝ TỰ THƯỜNG ───────────────────────────────────────────────
        if (key.length === 1) {
            // Gõ quá nhiều → tạo span extra
            if (charIndex >= currentWord.length) {
                const extraSpan = document.createElement('span');
                extraSpan.className = 'mt-char extra incorrect';
                extraSpan.textContent = key;
                currentWordDiv.appendChild(extraSpan);
                charIndex++;
                incorrectChars++;
                totalChars++;
                updateCaret();
                return;
            }

            const targetChar = currentWord[charIndex];
            const span = chars[charIndex];

            if (key === targetChar) {
                span.classList.add('correct');
                correctChars++;
            } else {
                span.classList.add('incorrect');
                incorrectChars++;
            }

            totalChars++;
            charIndex++;

            // Gõ xong từ cuối → kết thúc
            if (wordIndex === words.length - 1 && charIndex >= currentWord.length) {
                finishTest();
                return;
            }

            updateCaret();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TIMER: đếm tiến mỗi giây, ghi log WPM
    // ═══════════════════════════════════════════════════════════════════════
    /**
     * ── GHI LOG WPM MỖI GIÂY ──
     * Mỗi giây tính WPM hiện tại và push vào wpmHistory[].
     * Mảng này là data source cho trục Y của biểu đồ Chart.js.
     * Trục X = index + 1 (giây thứ 1, 2, 3, ...).
     */
    function startTimer() {
        timerInterval = setInterval(() => {
            elapsedSeconds++;

            const minutes = elapsedSeconds / 60;
            const wpm = minutes > 0 ? Math.round((correctChars / 5) / minutes) : 0;
            const rawWpm = minutes > 0 ? Math.round((totalChars / 5) / minutes) : 0;

            wpmHistory.push(wpm);
            rawWpmHistory.push(rawWpm);

            if (liveWpm) liveWpm.textContent = wpm;
        }, 1000);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KẾT THÚC TEST — tính kết quả, chuyển sang result phase
    // ═══════════════════════════════════════════════════════════════════════
    function finishTest() {
        isFinished = true;
        if (timerInterval) { clearInterval(timerInterval); timerInterval = null; }

        // ── Tính kết quả cuối cùng ──
        const minutes = (Date.now() - startTime) / 60000;
        const finalWpm = minutes > 0 ? Math.round((correctChars / 5) / minutes) : 0;
        const finalRaw = minutes > 0 ? Math.round((totalChars / 5) / minutes) : 0;
        const accuracy = totalChars > 0 ? Math.round((correctChars / totalChars) * 100) : 100;
        const totalTime = Math.round((Date.now() - startTime) / 1000);

        // Ẩn typing phase
        typingPhase.style.display = 'none';
        caretEl.style.display = 'none';

        // Hiện result phase
        resultPhase.classList.add('show');

        // Điền data
        const el = (id) => document.getElementById(id);
        if (el('resWpm')) el('resWpm').textContent = finalWpm;
        if (el('resAcc')) el('resAcc').textContent = accuracy + '%';
        if (el('resRaw')) el('resRaw').textContent = finalRaw;
        if (el('resCharsCorrect')) el('resCharsCorrect').textContent = correctChars;
        if (el('resCharsIncorrect')) el('resCharsIncorrect').textContent = incorrectChars;
        if (el('resTime')) el('resTime').textContent = totalTime + 's';

        drawChart();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VẼ BIỂU ĐỒ CHART.JS — Màu hồng/tím brand (#fb7aec)
    // ═══════════════════════════════════════════════════════════════════════
    /**
     * ── CẤU HÌNH CHART ──
     * - Trục X: giây (1, 2, 3, ...)
     * - Trục Y: WPM
     * - Đường WPM chính: màu hồng brand (#fb7aec)
     * - Đường Raw WPM: xám muted, nét đứt
     * - Đọc màu từ CSS variables để tương thích dark/light mode:
     *   + Lấy getComputedStyle() cho --color-muted và --color-charcoal
     */
    function drawChart() {
        if (!chartCanvas || typeof Chart === 'undefined') return;

        if (chartInstance) chartInstance.destroy();

        // Đọc màu từ CSS variables để đồng bộ theme
        const style = getComputedStyle(document.documentElement);
        const mutedColor = style.getPropertyValue('--color-muted').trim() || '#9a9a97';
        const gridColor = document.documentElement.classList.contains('dark')
            ? 'rgba(240, 237, 230, 0.05)'
            : 'rgba(0, 0, 0, 0.06)';
        const tooltipBg = document.documentElement.classList.contains('dark')
            ? '#2e2e33' : '#fff';
        const tooltipText = document.documentElement.classList.contains('dark')
            ? '#f0ede6' : '#2e2e33';

        const labels = wpmHistory.map((_, i) => i + 1);

        chartInstance = new Chart(chartCanvas, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'WPM',
                        data: wpmHistory,
                        borderColor: BRAND_PINK,
                        backgroundColor: BRAND_PINK_ALPHA,
                        borderWidth: 2.5,
                        pointRadius: 0,
                        pointHoverRadius: 5,
                        pointHoverBackgroundColor: BRAND_PINK,
                        tension: 0.3,
                        fill: true,
                    },
                    {
                        label: 'Raw',
                        data: rawWpmHistory,
                        borderColor: mutedColor,
                        borderWidth: 1.5,
                        borderDash: [5, 5],
                        pointRadius: 0,
                        pointHoverRadius: 4,
                        pointHoverBackgroundColor: mutedColor,
                        tension: 0.3,
                        fill: false,
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false,
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            color: mutedColor,
                            font: { family: "'Roboto Mono', monospace", size: 11 },
                            boxWidth: 20,
                            padding: 12,
                        }
                    },
                    tooltip: {
                        backgroundColor: tooltipBg,
                        titleColor: tooltipText,
                        bodyColor: tooltipText,
                        borderColor: mutedColor,
                        borderWidth: 0.5,
                        titleFont: { family: "'Roboto Mono', monospace" },
                        bodyFont: { family: "'Roboto Mono', monospace" },
                        callbacks: {
                            title: (ctx) => 'giây ' + ctx[0].label,
                        }
                    }
                },
                scales: {
                    x: {
                        title: {
                            display: true, text: 'giây', color: mutedColor,
                            font: { family: "'Roboto Mono', monospace", size: 11 }
                        },
                        ticks: { color: mutedColor, font: { size: 10 } },
                        grid: { color: gridColor },
                    },
                    y: {
                        title: {
                            display: true, text: 'wpm', color: mutedColor,
                            font: { family: "'Roboto Mono', monospace", size: 11 }
                        },
                        ticks: { color: mutedColor, font: { size: 10 } },
                        grid: { color: gridColor },
                        beginAtZero: true,
                    }
                }
            }
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KHỞI CHẠY
    // ═══════════════════════════════════════════════════════════════════════
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
