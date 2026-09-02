window.tm = {
    isDark() {
        return document.documentElement.classList.contains('dark');
    },
    toggleTheme() {
        const dark = document.documentElement.classList.toggle('dark');
        try { localStorage.setItem('tm-theme', dark ? 'dark' : 'light'); } catch (e) { }
        return dark;
    },
    focus(el) { el?.focus(); },
};
