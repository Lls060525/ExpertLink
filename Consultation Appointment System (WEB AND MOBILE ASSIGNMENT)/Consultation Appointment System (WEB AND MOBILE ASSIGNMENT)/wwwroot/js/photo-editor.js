// ===========================================================================
// Self-written photo editor (no library):
//   - choose a file or capture from the webcam (getUserMedia)
//   - drag to pan, wheel/slider to zoom, rotate 90°, flip, black & white
//   - exports a 400x400 JPEG back into the form's <input type=file>
// ===========================================================================
(function () {
    'use strict';
    const modalEl = document.getElementById('photoEditor');
    if (!modalEl) return;

    const input = document.getElementById(modalEl.dataset.input);
    const canvas = document.getElementById('peCanvas');
    const ctx = canvas.getContext('2d');
    const video = document.getElementById('peVideo');
    const zoomEl = document.getElementById('peZoom');
    const grayEl = document.getElementById('peGray');
    const applyBtn = document.getElementById('peApply');
    const snapBtn = document.getElementById('peSnap');
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const S = canvas.width;
    const OUT = 400;

    let img = null, zoom = 1, rot = 0, flip = false, gray = false, offX = 0, offY = 0, stream = null;

    function reset() {
        zoom = 1; rot = 0; flip = false; offX = 0; offY = 0; gray = false;
        zoomEl.value = 1; grayEl.checked = false;
    }

    function draw(target, size) {
        target.save();
        target.fillStyle = '#fff';
        target.fillRect(0, 0, size, size);
        if (img) {
            const k = size / S;
            const w = img.videoWidth || img.width, h = img.videoHeight || img.height;
            const scale = Math.max(size / w, size / h) * zoom;   // "cover" the square
            target.translate(size / 2 + offX * k, size / 2 + offY * k);
            target.rotate(rot * Math.PI / 180);
            target.scale(flip ? -1 : 1, 1);
            if (gray) target.filter = 'grayscale(1)';
            target.drawImage(img, -w * scale / 2, -h * scale / 2, w * scale, h * scale);
        }
        target.restore();
    }
    const render = () => draw(ctx, S);

    function setImage(source) {
        img = source;
        reset();
        render();
        applyBtn.disabled = false;
    }

    function loadFile(file) {
        if (!file || !/^image\/(jpeg|png|webp)$/.test(file.type)) {
            app.toast('Please choose a JPG, PNG or WEBP image.', { type: 'danger', icon: 'bi-exclamation-triangle' });
            return;
        }
        const im = new Image();
        im.onload = () => setImage(im);
        im.src = URL.createObjectURL(file);
    }

    // ---------------------------------------------------------- webcam
    async function startCamera() {
        try {
            stream = await navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 640, facingMode: 'user' }, audio: false });
            video.srcObject = stream;
            video.classList.remove('d-none');
            snapBtn.classList.remove('d-none');
        } catch (err) {
            app.toast('Unable to access the webcam: ' + err.message, { type: 'danger', icon: 'bi-camera-video-off' });
        }
    }
    function stopCamera() {
        stream?.getTracks().forEach(t => t.stop());
        stream = null;
        video.classList.add('d-none');
        snapBtn.classList.add('d-none');
    }
    function capture() {
        const c = document.createElement('canvas');
        c.width = video.videoWidth; c.height = video.videoHeight;
        const cx = c.getContext('2d');
        cx.translate(c.width, 0); cx.scale(-1, 1);   // mirror like a selfie
        cx.drawImage(video, 0, 0);
        stopCamera();
        setImage(c);
    }

    // ---------------------------------------------------------- export
    function apply() {
        const out = document.createElement('canvas');
        out.width = OUT; out.height = OUT;
        draw(out.getContext('2d'), OUT);
        out.toBlob(blob => {
            const file = new File([blob], 'photo.jpg', { type: 'image/jpeg' });
            const dt = new DataTransfer();
            dt.items.add(file);
            input._fromEditor = true;
            input.files = dt.files;
            input.dispatchEvent(new Event('change', { bubbles: true }));
            modal.hide();
        }, 'image/jpeg', 0.9);
    }

    // ---------------------------------------------------------- events
    document.getElementById('peFile').addEventListener('change', e => { stopCamera(); loadFile(e.target.files[0]); e.target.value = ''; });
    document.getElementById('peCamera').addEventListener('click', startCamera);
    snapBtn.addEventListener('click', capture);
    document.getElementById('peRotL').addEventListener('click', () => { rot = (rot + 270) % 360; render(); });
    document.getElementById('peRotR').addEventListener('click', () => { rot = (rot + 90) % 360; render(); });
    document.getElementById('peFlip').addEventListener('click', () => { flip = !flip; render(); });
    document.getElementById('peReset').addEventListener('click', () => { reset(); render(); });
    zoomEl.addEventListener('input', () => { zoom = +zoomEl.value; render(); });
    grayEl.addEventListener('change', () => { gray = grayEl.checked; render(); });
    applyBtn.addEventListener('click', apply);

    let dragging = false, lastX = 0, lastY = 0;
    canvas.addEventListener('pointerdown', e => { dragging = true; lastX = e.clientX; lastY = e.clientY; canvas.setPointerCapture(e.pointerId); });
    canvas.addEventListener('pointermove', e => {
        if (!dragging) return;
        const ratio = S / canvas.getBoundingClientRect().width;
        offX += (e.clientX - lastX) * ratio; offY += (e.clientY - lastY) * ratio;
        lastX = e.clientX; lastY = e.clientY;
        render();
    });
    canvas.addEventListener('pointerup', () => dragging = false);
    canvas.addEventListener('wheel', e => {
        e.preventDefault();
        zoom = Math.min(4, Math.max(1, zoom - Math.sign(e.deltaY) * 0.1));
        zoomEl.value = zoom;
        render();
    }, { passive: false });

    modalEl.addEventListener('hidden.bs.modal', stopCamera);

    // Choosing a file directly in the form's input opens the editor with it
    input?.addEventListener('change', () => {
        if (input._fromEditor) { input._fromEditor = false; return; }
        const f = input.files[0];
        if (f && f.type.startsWith('image/')) { loadFile(f); modal.show(); }
    });

    render();
})();
