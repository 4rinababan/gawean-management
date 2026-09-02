(function () {
    function preferred() {
        try {
            const saved = localStorage.getItem('tm-theme');
            if (saved) return saved === 'dark';
        } catch (e) { }
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    function apply(dark) {
        document.documentElement.classList.toggle('dark', dark);
        return dark;
    }

    window.tm = {
        isDark() {
            return document.documentElement.classList.contains('dark');
        },
        toggleTheme() {
            const dark = apply(!window.tm.isDark());
            try { localStorage.setItem('tm-theme', dark ? 'dark' : 'light'); } catch (e) { }
            return dark;
        },
        // Enhanced navigation re-syncs <html> attributes from the server response, which does not
        // know the visitor's theme — without this the `dark` class is dropped on every navigation.
        restoreTheme() {
            apply(preferred());
        },
        focus(el) { el?.focus(); },
    };
})();
