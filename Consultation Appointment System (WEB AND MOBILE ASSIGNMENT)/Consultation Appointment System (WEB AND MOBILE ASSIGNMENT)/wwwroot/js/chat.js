// ===========================================================================
// Real-time appointment chat (SignalR hub /hub)
// ===========================================================================
(function () {
    'use strict';
    const box = document.getElementById('chatBox');
    if (!box) return;
    const app = window.app;
    const id = +box.dataset.appointmentId;
    const me = +document.body.dataset.userId;
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatInput');
    const typing = document.getElementById('typing');
    const status = document.getElementById('chatStatus');
    app.activeChatId = id;

    const scroll = () => box.scrollTop = box.scrollHeight;
    scroll();

    function add(m) {
        box.querySelector('.chat-empty')?.remove();
        const row = document.createElement('div');
        row.className = 'msg ' + (m.senderId === me ? 'me' : 'them');
        const img = document.createElement('img');
        img.className = 'avatar'; img.src = m.photo; img.alt = '';
        const wrap = document.createElement('div');
        const bubble = document.createElement('div');
        bubble.className = 'bubble'; bubble.textContent = m.content;
        const meta = document.createElement('div');
        meta.className = 'meta' + (m.senderId === me ? ' text-end' : '');
        meta.textContent = (m.senderId === me ? '' : m.sender + ' · ') + m.sentAt;
        wrap.append(bubble, meta);
        row.append(img, wrap);
        box.appendChild(row);
        scroll();
    }

    if (!app.hub || !app.hubReady) {
        status.textContent = 'Real-time chat unavailable (could not load SignalR).';
        form?.querySelectorAll('input,button').forEach(x => x.disabled = true);
        return;
    }

    if (!form) return;   // chat closed (cancelled / admin view) - history only

    const join = () => app.hub.invoke('JoinAppointment', id)
        .then(() => { status.innerHTML = '<i class="bi bi-circle-fill text-success" style="font-size:.5rem"></i> Live'; })
        .catch(err => { status.textContent = err.message; });

    app.hubReady.then(join);
    app.hub.onreconnected(join);
    app.hub.onreconnecting(() => status.textContent = 'Reconnecting...');

    app.hub.on('message', m => {
        add(m);
        if (m.senderId !== me) { typing.textContent = ''; app.beep(); }
    });

    let typingTimer;
    app.hub.on('typing', name => {
        typing.textContent = `${name} is typing...`;
        clearTimeout(typingTimer);
        typingTimer = setTimeout(() => typing.textContent = '', 2000);
    });

    let lastTyping = 0;
    input.addEventListener('input', () => {
        if (Date.now() - lastTyping > 1500) { lastTyping = Date.now(); app.hub.invoke('Typing', id).catch(() => { }); }
    });
    input.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); form.requestSubmit(); }
    });
    form.addEventListener('submit', async e => {
        e.preventDefault();
        const text = input.value.trim();
        if (!text) return;
        try {
            await app.hub.invoke('SendMessage', id, text);
            input.value = '';
            input.focus();
        } catch (err) {
            app.toast(err.message.replace(/^.*HubException: /, ''), { type: 'danger', icon: 'bi-exclamation-triangle' });
        }
    });
})();
