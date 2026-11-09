// ===========================================================================
// ExpertLink - global UI helpers (theme, toasts, confirm modal, AJAX POST,
// image preview, keyboard shortcuts, password visibility)
// ===========================================================================
(function () {
    'use strict';
    const app = window.app = window.app || {};

    app.token = () => document.querySelector('#antiForgery input[name="__RequestVerificationToken"]')?.value || '';

    // POST with anti-forgery token
    app.post = async function (url, data) {
        const body = new URLSearchParams(data || {});
        body.append('__RequestVerificationToken', app.token());
        const res = await fetch(url, { method: 'POST', body, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!res.ok) throw new Error('Request failed (' + res.status + ')');
        return (res.headers.get('content-type') || '').includes('json') ? res.json() : res.text();
    };

    // Escape text for safe HTML insertion
    app.esc = s => { const d = document.createElement('div'); d.textContent = s ?? ''; return d.innerHTML; };

    // Bootstrap toast
    app.toast = function (message, opts = {}) {
        const wrap = document.getElementById('toasts');
        if (!wrap) return;
        const el = document.createElement('div');
        el.className = 'toast align-items-center border-0 text-bg-' + (opts.type || 'dark');
        el.setAttribute('role', 'alert');
        el.innerHTML = '<div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
        const body = el.querySelector('.toast-body');
        const icon = document.createElement('i');
        icon.className = 'bi ' + (opts.icon || 'bi-bell') + ' me-2';
        body.append(icon, document.createTextNode(message));
        if (opts.url) { body.style.cursor = 'pointer'; body.addEventListener('click', () => location.href = opts.url); }
        wrap.appendChild(el);
        new bootstrap.Toast(el, { delay: opts.delay || 6000 }).show();
        el.addEventListener('hidden.bs.toast', () => el.remove());
    };

    // Notification sound generated with the Web Audio API (no audio file needed)
    app.beep = function () {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const o = ctx.createOscillator(), g = ctx.createGain();
            o.type = 'sine';
            o.frequency.setValueAtTime(880, ctx.currentTime);
            o.frequency.setValueAtTime(1320, ctx.currentTime + 0.12);
            g.gain.setValueAtTime(0.0001, ctx.currentTime);
            g.gain.exponentialRampToValueAtTime(0.2, ctx.currentTime + 0.02);
            g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.35);
            o.connect(g).connect(ctx.destination);
            o.start();
            o.stop(ctx.currentTime + 0.36);
        } catch (e) { /* audio not allowed yet */ }
    };

    // ------------------------------------------------------------------ theme
    const toggle = document.getElementById('themeToggle');
    function setTheme(t) {
        document.documentElement.setAttribute('data-bs-theme', t);
        const i = toggle?.querySelector('i');
        if (i) i.className = 'bi ' + (t === 'dark' ? 'bi-sun' : 'bi-moon-stars');
        app.post('/Home/SetTheme', { theme: t }).catch(() => { });
        document.dispatchEvent(new CustomEvent('themechange', { detail: t }));
    }
    app.toggleTheme = () => setTheme(document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark');
    toggle?.addEventListener('click', app.toggleTheme);

    // ------------------------------------------------------- confirm dialog
    let pendingForm = null;
    document.addEventListener('submit', e => {
        const f = e.target;
        if (f.matches('form[data-confirm]') && !f.dataset.confirmed) {
            e.preventDefault();
            pendingForm = f;
            document.getElementById('confirmText').textContent = f.dataset.confirm;
            bootstrap.Modal.getOrCreateInstance('#confirmModal').show();
        }
    }, true);
    document.getElementById('confirmOk')?.addEventListener('click', () => {
        if (!pendingForm) return;
        pendingForm.dataset.confirmed = '1';
        bootstrap.Modal.getInstance('#confirmModal')?.hide();
        pendingForm.requestSubmit ? pendingForm.requestSubmit() : pendingForm.submit();
        const f = pendingForm;
        setTimeout(() => delete f.dataset.confirmed, 500);
        pendingForm = null;
    });

    // ------------------------------------------------ prevent double submit
    $(document).on('submit', 'form:not([data-no-lock])', function (e) {
        if (e.isDefaultPrevented()) return;
        const $f = $(this);
        if ($f.data('validator') && !$f.valid()) return;
        setTimeout(() => $f.find('button[type=submit], button:not([type])').prop('disabled', true), 0);
    });
    window.addEventListener('pageshow', () => $('form button').prop('disabled', false));

    // ------------------------------------------------- auto-dismiss alerts
    document.querySelectorAll('.alert.auto-dismiss').forEach(a => {
        if (a.querySelector('a')) return;   // keep alerts with demo links
        setTimeout(() => bootstrap.Alert.getOrCreateInstance(a).close(), 6000);
    });

    // ----------------------------- image preview: <input type=file data-preview="#img">
    document.addEventListener('change', e => {
        const input = e.target;
        if (!input.matches('input[type=file][data-preview]')) return;
        const img = document.querySelector(input.dataset.preview);
        const f = input.files[0];
        if (img && f && f.type.startsWith('image/')) img.src = URL.createObjectURL(f);
    });

    // ------------------------------------------------ show / hide password
    document.addEventListener('click', e => {
        const btn = e.target.closest('.pw-toggle');
        if (!btn) return;
        const input = btn.parentElement.querySelector('input');
        const show = input.type === 'password';
        input.type = show ? 'text' : 'password';
        btn.querySelector('i').className = 'bi ' + (show ? 'bi-eye-slash' : 'bi-eye');
    });

    // ------------------------------------------------ keyboard shortcuts
    //   /        focus the search box on the page
    //   Shift+D  toggle dark mode
    document.addEventListener('keydown', e => {
        const tag = (e.target.tagName || '').toLowerCase();
        if (['input', 'textarea', 'select'].includes(tag) || e.target.isContentEditable) return;
        if (e.key === '/') {
            const s = document.querySelector('[data-search]');
            if (s) { e.preventDefault(); s.focus(); }
        } else if (e.key === 'D' && e.shiftKey) {
            app.toggleTheme();
        }
    });

    // ------------------------------------------------ tooltips
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => new bootstrap.Tooltip(el));

    // ------------------------------------------------ debounce helper
    app.debounce = (fn, ms = 350) => { let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); }; };
})();
