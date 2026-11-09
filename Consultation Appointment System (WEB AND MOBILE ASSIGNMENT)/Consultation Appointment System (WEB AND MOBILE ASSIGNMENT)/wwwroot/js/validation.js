// ===========================================================================
// Client-side rules for the custom server-side validation attributes
// (Models/ValidationAttributes.cs) - jQuery unobtrusive adapters
// ===========================================================================
(function ($) {
    'use strict';

    // [MustBeTrue] - checkbox must be ticked
    $.validator.addMethod('mustbetrue', (value, el) => el.checked);
    $.validator.unobtrusive.adapters.addBool('mustbetrue');

    // [FutureDate(maxDays)] - yyyy-MM-dd between today and today + max days
    $.validator.addMethod('futuredate', function (value, el, params) {
        if (!value) return true;
        const d = new Date(value + 'T00:00:00');
        if (isNaN(d)) return false;
        const today = new Date(); today.setHours(0, 0, 0, 0);
        const max = new Date(today); max.setDate(max.getDate() + parseInt(params.max || 365, 10));
        return d >= today && d <= max;
    });
    $.validator.unobtrusive.adapters.add('futuredate', ['max'], function (o) {
        o.rules.futuredate = { max: o.params.max };
        o.messages.futuredate = o.message;
    });

    // [Luhn] - card number checksum
    $.validator.addMethod('luhn', function (value) {
        if (!value) return true;
        const digits = value.replace(/[\s-]/g, '');
        if (!/^\d{13,19}$/.test(digits)) return false;
        let sum = 0, dbl = false;
        for (let i = digits.length - 1; i >= 0; i--) {
            let n = +digits[i];
            if (dbl) { n *= 2; if (n > 9) n -= 9; }
            sum += n; dbl = !dbl;
        }
        return sum % 10 === 0;
    });
    $.validator.unobtrusive.adapters.addBool('luhn');

    // [CardExpiry] - MM/YY and not expired
    $.validator.addMethod('cardexpiry', function (value) {
        if (!value) return true;
        const m = /^(0[1-9]|1[0-2])\/(\d{2})$/.exec(value.trim());
        if (!m) return false;
        const lastDay = new Date(2000 + +m[2], +m[1], 0, 23, 59, 59);
        return lastDay >= new Date();
    });
    $.validator.unobtrusive.adapters.addBool('cardexpiry');

    // [MaxFileSize(mb)] - every selected file must be within the limit
    $.validator.addMethod('filesize', function (value, el, max) {
        if (!el.files || el.files.length === 0) return true;
        return [...el.files].every(f => f.size <= +max);
    });
    $.validator.unobtrusive.adapters.addSingleVal('filesize', 'max');

    // Bootstrap styling for invalid fields
    $.validator.setDefaults({
        highlight: el => $(el).addClass('is-invalid'),
        unhighlight: el => $(el).removeClass('is-invalid')
    });
})(jQuery);
