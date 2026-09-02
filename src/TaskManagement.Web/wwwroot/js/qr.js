// Renders the authenticator otpauth:// URI as a QR code on the 2FA setup page.
// The page is statically rendered by the Identity scaffolding, so this runs on load and again after
// each enhanced navigation rather than through component interop.
(function () {
    function render() {
        const target = document.getElementById('qrCode');
        const data = document.getElementById('qrCodeData');
        if (!target || !data || target.hasChildNodes()) return;

        const url = data.getAttribute('data-url');
        if (!url || typeof QRCode === 'undefined') return;

        new QRCode(target, {
            text: url,
            width: 180,
            height: 180,
            correctLevel: QRCode.CorrectLevel.M,
        });
    }

    document.addEventListener('DOMContentLoaded', render);
    if (document.readyState !== 'loading') render();
    window.tmRenderQrCode = render;
})();
