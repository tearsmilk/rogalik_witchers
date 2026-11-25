// Рендерит build/icon.svg в build/icon.png (1024x1024) через Playwright.
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1024, height: 1024 } });
  await p.goto('file://' + path.resolve(__dirname, '../build/icon.svg'));
  await p.screenshot({ path: path.resolve(__dirname, '../build/icon.png'), omitBackground: true });
  await b.close();
})();
