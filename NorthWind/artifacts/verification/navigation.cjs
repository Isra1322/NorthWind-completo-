const { chromium } = require('playwright');
const fs = require('node:fs');

(async () => {
    const browser = await chromium.launch({ channel: 'chrome', headless: true });
    try {
        const page = await browser.newPage();
        await page.goto('https://localhost:7154/swagger/index.html');
        await page.locator('.opblock-post').filter({ hasText: '/CreateOrder' }).waitFor();
        await page.screenshot({ path: 'artifacts/verification/swagger.png', fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 });
        await page.goto('https://localhost:7254/');
        await page.getByRole('heading', { name: 'NorthWind Sales' }).waitFor();
        await page.getByRole('button', { name: 'Alternar navegación' }).click();
        await page.getByRole('link', { name: 'Crear orden', exact: true }).click();
        await page.getByRole('heading', { name: 'Crear orden de compra' }).waitFor();
        const overflow = await page.evaluate(() =>
            document.documentElement.scrollWidth > window.innerWidth);
        if (overflow) throw new Error('El diseño móvil tiene desbordamiento horizontal');
        await page.screenshot({ path: 'artifacts/verification/mobile.png', fullPage: true });
        const result = { swaggerPostCreateOrderVisible: true,
            mobileNavigationPassed: true, horizontalOverflow: overflow,
            certificateErrorsIgnored: false };
        fs.writeFileSync('artifacts/verification/navigation-result.json', JSON.stringify(result, null, 2));
        console.log(JSON.stringify(result));
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
