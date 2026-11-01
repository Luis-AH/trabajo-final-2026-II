(() => {
    'use strict';
    const form = document.getElementById('account');
    if (!form) return;
    const password = document.getElementById('Input_Password');
    const toggle = document.getElementById('password-toggle');
    const warning = document.getElementById('caps-warning');
    const button = document.getElementById('login-submit');
    const label = document.getElementById('login-label');
    if (password && toggle) {
        toggle.hidden = false;
        toggle.addEventListener('click', () => {
            const visible = password.type === 'password';
            password.type = visible ? 'text' : 'password';
            toggle.textContent = visible ? 'Ocultar' : 'Mostrar';
            toggle.setAttribute('aria-pressed', String(visible));
            password.focus();
        });
        password.addEventListener('keyup', event => {
            if (warning) warning.hidden = !(event.getModifierState && event.getModifierState('CapsLock'));
        });
        password.addEventListener('blur', () => { if (warning) warning.hidden = true; });
    }
    form.addEventListener('submit', event => {
        const valid = form.checkValidity() && (!window.jQuery || !window.jQuery.fn.valid || window.jQuery(form).valid());
        if (!valid || event.defaultPrevented) return;
        if (button) { button.disabled = true; button.setAttribute('aria-busy', 'true'); }
        if (label) label.textContent = 'Iniciando sesión…';
    });
    window.addEventListener('pageshow', () => {
        if (button) { button.disabled = false; button.removeAttribute('aria-busy'); }
        if (label) label.textContent = 'Ingresar a SegundaOportunidad';
        if (password) password.type = 'password';
        if (toggle) { toggle.textContent = 'Mostrar'; toggle.setAttribute('aria-pressed', 'false'); }
    });
})();
