// ===========================================================================
// Real-time notifications with SignalR + navbar bell
// ===========================================================================
(function () {
    'use strict';
    const app = window.app;
    const list = document.getElementById('notifList');
    const badge = document.getElementById('bellCount');

    async function load() {
        try {
            const r = await fetch('/Notification/Latest', { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!r.ok) return;
            const data = await r.json();
            badge.textContent = data.unread > 9 ? '9+' : data.unread;
            badge.classList.toggle('d-none', data.unread === 0);
            list.innerHTML = data.items.length === 0
                ? '<div class="p-3 text-muted small">No notifications yet.</div>'
                : data.items.map(n =>
                    `<a class="notif-item ${n.isRead ? '' : 'unread'}" href="/Notification/Open/${n.id}">${app.esc(n.message)}
                       <div class="text-muted" style="font-size:.75rem">${app.esc(n.time)}</div></a>`).join('');
        } catch (e) { /* ignore */ }
    }
    app.loadNotifications = load;

    document.getElementById('markAllRead')?.addEventListener('click', async e => {
        e.preventDefault();
        e.stopPropagation();
        await app.post('/Notification/MarkAllRead');
        load();
    });

    if (window.signalR) {
        const conn = new signalR.HubConnectionBuilder()
            .withUrl('/hub')
            .withAutomaticReconnect()
            .build();
        app.hub = conn;

        conn.on('notify', n => {
            app.beep();
            app.toast(n.message, { url: n.url, icon: 'bi-bell-fill', type: 'primary' });
            load();
        });

        conn.on('chat', n => {
            if (app.activeChatId === n.appointmentId) return;   // already looking at this chat
            app.beep();
            app.toast(n.message, { url: n.url, icon: 'bi-chat-dots-fill' });
        });

        app.hubReady = conn.start().catch(err => console.warn('SignalR connection failed', err));
    }

    load();
})();
