/**
 * ============================================================================
 * BPCVN — Waveform Audio Player (WaveSurfer.js)
 * ============================================================================
 *
 * File này khởi tạo WaveSurfer.js cho tất cả các phần tử có class ".waveform"
 * trên trang. Thay thế thẻ <audio> mặc định bằng giao diện sóng âm (waveform)
 * giống SoundCloud, mang lại UX trực quan hơn cho cộng đồng BPCVN.
 *
 * FLOW LOGIC CHÍNH:
 * 1. Tìm tất cả các container ".waveform-player" trên trang.
 * 2. Với mỗi container, đọc URL audio từ data-attribute của thẻ .waveform.
 * 3. Khởi tạo instance WaveSurfer tương ứng.
 * 4. Gắn sự kiện click cho nút Play/Pause.
 * 5. QUAN TRỌNG: Khi nhấn Play ở bất kỳ instance nào, tất cả instance khác
 *    sẽ bị pause — đảm bảo chỉ có 1 âm thanh phát tại một thời điểm.
 *
 * KHÔNG SỬ DỤNG jQuery — chỉ Vanilla JS thuần.
 * ============================================================================
 */

// --- Nhập WaveSurfer từ CDN (ES Module) ---
import WaveSurfer from 'https://cdn.jsdelivr.net/npm/wavesurfer.js@7/dist/wavesurfer.esm.js';

/**
 * Mảng global lưu trữ tất cả các instance WaveSurfer trên trang.
 * Dùng để lặp qua và pause các instance khác khi 1 instance được play.
 */
const wavesurferInstances = [];

/**
 * Kiểm tra trang hiện tại đang ở chế độ Dark Mode hay không.
 * Dựa vào class "dark" trên thẻ <html> (do theme toggle trong _Layout.cshtml quản lý).
 *
 * @returns {boolean} true nếu đang ở dark mode
 */
function isDarkMode() {
    return document.documentElement.classList.contains('dark');
}

/**
 * Trả về bộ màu waveform phù hợp với chế độ hiện tại (dark/light).
 * - Light mode: sóng xám nhạt, progress cam ấm
 * - Dark mode: sóng xám đậm, progress hồng (#fb7aec — brand color)
 *
 * @returns {Object} { waveColor, progressColor, cursorColor }
 */
function getWaveColors() {
    if (isDarkMode()) {
        return {
            waveColor: 'rgba(240, 237, 230, 0.25)',     // Sóng chưa phát — xám sáng nhẹ
            progressColor: '#fb7aec',                     // Sóng đã phát — hồng brand
            cursorColor: '#fb7aec'                        // Đường cursor — hồng brand
        };
    }
    return {
        waveColor: 'rgba(28, 28, 28, 0.2)',              // Sóng chưa phát — xám nhạt
        progressColor: '#f97316',                         // Sóng đã phát — cam ấm (orange-500)
        cursorColor: '#f97316'                            // Đường cursor — cam ấm
    };
}

/**
 * ============================================================================
 * HÀM CHÍNH: Khởi tạo tất cả Waveform Player trên trang
 * ============================================================================
 *
 * Quy trình:
 * 1. Quét tất cả các element có class "waveform-player" (container bao ngoài).
 * 2. Trong mỗi container, tìm:
 *    - Thẻ .waveform (div chứa sóng âm) → đọc data-audio-url để lấy URL file audio.
 *    - Nút .waveform-play-btn (nút Play/Pause).
 * 3. Tạo instance WaveSurfer, load audio, và gắn sự kiện.
 */
function initWaveformPlayers() {
    // Tìm tất cả container waveform trên trang
    const players = document.querySelectorAll('.waveform-player');

    players.forEach((playerEl) => {
        const waveformDiv = playerEl.querySelector('.waveform');
        const playBtn = playerEl.querySelector('.waveform-play-btn');

        // Kiểm tra dữ liệu hợp lệ — bỏ qua nếu thiếu element hoặc URL
        if (!waveformDiv || !playBtn) return;

        const audioUrl = waveformDiv.dataset.audioUrl;
        if (!audioUrl) return;

        // Lấy bộ màu phù hợp với theme hiện tại
        const colors = getWaveColors();

        // --- Khởi tạo instance WaveSurfer ---
        const wavesurfer = WaveSurfer.create({
            container: waveformDiv,          // DOM element để render sóng âm
            url: audioUrl,                    // URL file audio cần load
            waveColor: colors.waveColor,      // Màu sóng chưa phát
            progressColor: colors.progressColor, // Màu sóng đã phát
            cursorColor: colors.cursorColor,  // Màu đường cursor
            cursorWidth: 2,                   // Độ rộng cursor (px)
            barWidth: 3,                      // Độ rộng mỗi thanh sóng (px)
            barGap: 2,                        // Khoảng cách giữa các thanh sóng (px)
            barRadius: 3,                     // Bo góc thanh sóng (px)
            height: 48,                       // Chiều cao waveform (px)
            responsive: true,                 // Tự động resize theo container
            normalize: true,                  // Chuẩn hóa biên độ sóng
            hideScrollbar: true,              // Ẩn scrollbar ngang
        });

        // Lưu instance vào mảng global để quản lý cross-instance
        wavesurferInstances.push({ wavesurfer, playBtn });

        /**
         * ────────────────────────────────────────────────────────
         * SỰ KIỆN: Click nút Play/Pause
         * ────────────────────────────────────────────────────────
         *
         * LOGIC NGẮT ÂM THANH CHÉO (Cross-instance pause):
         * Trước khi play instance hiện tại, lặp qua TẤT CẢ instance khác
         * trong mảng wavesurferInstances và gọi .pause().
         * → Đảm bảo chỉ có DUY NHẤT 1 bản record phát tại một thời điểm.
         *
         * Sau đó toggle play/pause cho instance hiện tại.
         */
        playBtn.addEventListener('click', () => {
            // Nếu instance hiện tại ĐANG KHÔNG phát → sắp phát → pause tất cả instance khác
            if (!wavesurfer.isPlaying()) {
                wavesurferInstances.forEach((item) => {
                    if (item.wavesurfer !== wavesurfer && item.wavesurfer.isPlaying()) {
                        item.wavesurfer.pause();
                        // Cập nhật icon nút Play của instance bị pause
                        updatePlayBtnIcon(item.playBtn, false);
                    }
                });
            }

            // Toggle play/pause cho instance hiện tại
            wavesurfer.playPause();
        });

        /**
         * ────────────────────────────────────────────────────────
         * SỰ KIỆN: Cập nhật icon nút khi trạng thái phát thay đổi
         * ────────────────────────────────────────────────────────
         * WaveSurfer phát ra event "play" và "pause" khi trạng thái thay đổi.
         * Lắng nghe để cập nhật icon Bootstrap tương ứng.
         */
        wavesurfer.on('play', () => {
            updatePlayBtnIcon(playBtn, true);
        });

        wavesurfer.on('pause', () => {
            updatePlayBtnIcon(playBtn, false);
        });

        /**
         * ────────────────────────────────────────────────────────
         * SỰ KIỆN: Khi audio phát xong (finish)
         * ────────────────────────────────────────────────────────
         * Reset icon về trạng thái Play.
         */
        wavesurfer.on('finish', () => {
            updatePlayBtnIcon(playBtn, false);
        });

        /**
         * ────────────────────────────────────────────────────────
         * SỰ KIỆN: Khi waveform đang loading
         * ────────────────────────────────────────────────────────
         * Hiển thị trạng thái loading trên nút Play.
         */
        wavesurfer.on('loading', (percent) => {
            if (percent < 100) {
                playBtn.disabled = true;
                playBtn.innerHTML = '<i class="bi bi-hourglass-split waveform-icon-loading"></i>';
            }
        });

        /**
         * ────────────────────────────────────────────────────────
         * SỰ KIỆN: Khi waveform đã sẵn sàng (ready)
         * ────────────────────────────────────────────────────────
         * Bật nút Play và hiển thị thời lượng audio.
         */
        wavesurfer.on('ready', () => {
            playBtn.disabled = false;
            updatePlayBtnIcon(playBtn, false);

            // Hiển thị thời lượng tổng cộng
            const durationEl = playerEl.querySelector('.waveform-duration');
            if (durationEl) {
                durationEl.textContent = formatTime(wavesurfer.getDuration());
            }
        });

        /**
         * ────────────────────────────────────────────────────────
         * SỰ KIỆN: Cập nhật thời gian hiện tại khi đang phát
         * ────────────────────────────────────────────────────────
         */
        wavesurfer.on('timeupdate', (currentTime) => {
            const currentEl = playerEl.querySelector('.waveform-current-time');
            if (currentEl) {
                currentEl.textContent = formatTime(currentTime);
            }
        });
    });
}

/**
 * Cập nhật icon Bootstrap của nút Play/Pause.
 *
 * @param {HTMLElement} btn — Thẻ <button> chứa icon
 * @param {boolean} isPlaying — true = đang phát (hiển thị icon Pause),
 *                               false = đang dừng (hiển thị icon Play)
 */
function updatePlayBtnIcon(btn, isPlaying) {
    if (isPlaying) {
        btn.innerHTML = '<i class="bi bi-pause-fill"></i>';
        btn.classList.add('playing');
        btn.title = 'Pause';
    } else {
        btn.innerHTML = '<i class="bi bi-play-fill"></i>';
        btn.classList.remove('playing');
        btn.title = 'Play';
    }
}

/**
 * Chuyển đổi giây thành chuỗi định dạng mm:ss.
 *
 * @param {number} seconds — Số giây
 * @returns {string} — Chuỗi "mm:ss" (ví dụ: "01:23")
 */
function formatTime(seconds) {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
}

/**
 * ============================================================================
 * THEO DÕI THAY ĐỔI THEME (Dark ↔ Light)
 * ============================================================================
 * Khi người dùng bật/tắt dark mode, cập nhật màu sóng cho tất cả instance.
 * Sử dụng MutationObserver để theo dõi sự thay đổi class trên thẻ <html>.
 */
const themeObserver = new MutationObserver(() => {
    const colors = getWaveColors();
    wavesurferInstances.forEach((item) => {
        item.wavesurfer.setOptions({
            waveColor: colors.waveColor,
            progressColor: colors.progressColor,
            cursorColor: colors.cursorColor,
        });
    });
});

// Bắt đầu theo dõi thay đổi attribute "class" trên <html>
themeObserver.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['class'],
});

/**
 * ============================================================================
 * KHỞI CHẠY: Chờ DOM sẵn sàng rồi khởi tạo tất cả waveform player
 * ============================================================================
 */
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initWaveformPlayers);
} else {
    // DOM đã sẵn sàng (trường hợp script được load defer/async)
    initWaveformPlayers();
}
