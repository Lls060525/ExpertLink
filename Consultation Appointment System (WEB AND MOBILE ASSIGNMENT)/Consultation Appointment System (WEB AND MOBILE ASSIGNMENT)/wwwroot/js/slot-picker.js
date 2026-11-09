// ===========================================================================
// Interactive on-screen calendar + time-slot picker (Booking / Reschedule)
//   container: <div id="slotPicker" data-service-id data-exclude data-max-days>
//   hidden inputs: #Date (yyyy-MM-dd) and #StartTime (HH:mm)
// ===========================================================================
(function () {
    'use strict';
    const root = document.getElementById('slotPicker');
    if (!root) return;

    const serviceId = root.dataset.serviceId;
    const exclude = root.dataset.exclude || 0;
    const maxDays = +(root.dataset.maxDays || 60);
    const dateInput = document.getElementById('Date');
    const timeInput = document.getElementById('StartTime');
    const calEl = root.querySelector('.cal-grid');
    const titleEl = root.querySelector('.cal-title');
    const slotsEl = document.getElementById('slots');
    const slotTitle = document.getElementById('slotTitle');
    const sumDate = document.getElementById('sumDate');
    const sumTime = document.getElementById('sumTime');

    const pad = n => String(n).padStart(2, '0');
    const iso = d => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const lastDay = new Date(today); lastDay.setDate(lastDay.getDate() + maxDays);

    let selected = dateInput.value || null;
    let view = selected ? new Date(selected + 'T00:00:00') : new Date(today);
    view.setDate(1);
    let counts = {};

    async function loadMonth() {
        titleEl.textContent = view.toLocaleDateString('en-MY', { month: 'long', year: 'numeric' });
        renderCalendar(true);
        try {
            const r = await fetch(`/Booking/Month?serviceId=${serviceId}&year=${view.getFullYear()}&month=${view.getMonth() + 1}&exclude=${exclude}`);
            counts = r.ok ? await r.json() : {};
        } catch { counts = {}; }
        renderCalendar(false);
    }

    function renderCalendar(loading) {
        calEl.innerHTML = '';
        ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].forEach(d => {
            const h = document.createElement('div'); h.className = 'cal-dow'; h.textContent = d; calEl.appendChild(h);
        });
        const offset = (view.getDay() + 6) % 7;   // Monday first
        for (let i = 0; i < offset; i++) calEl.appendChild(document.createElement('div'));

        const month = view.getMonth();
        for (let d = new Date(view); d.getMonth() === month; d.setDate(d.getDate() + 1)) {
            const key = iso(d);
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'cal-day';
            btn.dataset.date = key;
            btn.innerHTML = `${d.getDate()}`;
            const outOfRange = d < today || d > lastDay;
            const free = counts[key];
            if (outOfRange || (!loading && !free)) {
                btn.classList.add('disabled');
                btn.disabled = true;
                if (!outOfRange && !loading) btn.title = 'No available slots';
            } else if (!loading) {
                btn.insertAdjacentHTML('beforeend', `<small>${free} slot${free > 1 ? 's' : ''}</small>`);
            }
            if (+d === +today) btn.classList.add('today');
            if (key === selected) btn.classList.add('selected');
            btn.addEventListener('click', () => selectDate(key));
            calEl.appendChild(btn);
        }
        root.querySelector('.cal-prev').disabled = view <= new Date(today.getFullYear(), today.getMonth(), 1);
        root.querySelector('.cal-next').disabled = new Date(view.getFullYear(), view.getMonth() + 1, 1) > lastDay;
    }

    async function selectDate(key, keepTime) {
        selected = key;
        dateInput.value = key;
        if (!keepTime) timeInput.value = '';
        calEl.querySelectorAll('.cal-day').forEach(b => b.classList.toggle('selected', b.dataset.date === key));
        slotsEl.innerHTML = '<div class="text-muted small"><span class="spinner-border spinner-border-sm"></span> Loading slots...</div>';
        updateSummary();
        try {
            const r = await fetch(`/Booking/Slots?serviceId=${serviceId}&date=${key}&exclude=${exclude}`);
            const data = await r.json();
            slotTitle.textContent = data.date;
            renderSlots(data);
        } catch {
            slotsEl.innerHTML = '<div class="text-danger small">Unable to load slots. Please try again.</div>';
        }
    }

    function renderSlots(data) {
        slotsEl.innerHTML = '';
        if (data.off) { slotsEl.innerHTML = '<div class="empty-state py-3"><i class="bi bi-calendar-x"></i>The consultant is on leave on this day.</div>'; return; }
        if (data.slots.length === 0) { slotsEl.innerHTML = '<div class="empty-state py-3"><i class="bi bi-moon"></i>The consultant does not work on this day.</div>'; return; }

        const groups = { Morning: [], Afternoon: [], Evening: [] };
        data.slots.forEach(s => {
            const h = +s.start.substring(0, 2);
            (h < 12 ? groups.Morning : h < 17 ? groups.Afternoon : groups.Evening).push(s);
        });
        Object.entries(groups).forEach(([name, list]) => {
            if (list.length === 0) return;
            const title = document.createElement('div');
            title.className = 'small fw-semibold text-muted mb-2 mt-2';
            title.innerHTML = `<i class="bi ${name === 'Morning' ? 'bi-sunrise' : name === 'Afternoon' ? 'bi-sun' : 'bi-moon-stars'}"></i> ${name}`;
            const grid = document.createElement('div');
            grid.className = 'slot-grid';
            list.forEach(s => {
                const b = document.createElement('button');
                b.type = 'button';
                b.className = 'slot';
                b.textContent = s.label;
                b.title = s.available ? `${s.label} - available` : 'Not available';
                b.disabled = !s.available;
                b.dataset.start = s.start;
                if (s.start === timeInput.value) b.classList.add('selected');
                b.addEventListener('click', () => {
                    slotsEl.querySelectorAll('.slot').forEach(x => x.classList.remove('selected'));
                    b.classList.add('selected');
                    timeInput.value = s.start;
                    sumTime.dataset.label = `${s.label} - ${to12(s.end)}`;
                    updateSummary();
                    document.getElementById('StartTime-error')?.remove();
                });
                grid.appendChild(b);
            });
            slotsEl.append(title, grid);
        });
        if (!data.slots.some(s => s.available))
            slotsEl.insertAdjacentHTML('afterbegin', '<div class="alert alert-warning py-2 small">All slots on this day are taken. Please choose another date.</div>');
        const sel = slotsEl.querySelector('.slot.selected');
        if (sel) sumTime.dataset.label = sel.title.split(' - ')[0];
        updateSummary();
    }

    function to12(hhmm) {
        let [h, m] = hhmm.split(':').map(Number);
        const ap = h >= 12 ? 'PM' : 'AM';
        h = h % 12 || 12;
        return `${h}:${pad(m)} ${ap}`;
    }

    function updateSummary() {
        sumDate.textContent = selected ? new Date(selected + 'T00:00:00').toLocaleDateString('en-MY', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }) : '-';
        sumTime.textContent = timeInput.value ? (sumTime.dataset.label || to12(timeInput.value)) : '-';
        document.getElementById('bookBtn')?.toggleAttribute('disabled', !(selected && timeInput.value));
    }

    root.querySelector('.cal-prev').addEventListener('click', () => { view.setMonth(view.getMonth() - 1); loadMonth(); });
    root.querySelector('.cal-next').addEventListener('click', () => { view.setMonth(view.getMonth() + 1); loadMonth(); });

    loadMonth();
    if (selected) selectDate(selected, true);
    updateSummary();
})();
