// Real HTTPS panel and Unix-socket backend, with BrowserFixtureHost synthetic data.
import { chromium } from '../../outputs/es-browser-tools/node_modules/playwright/index.mjs';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';

const fixture = JSON.parse(await fs.readFile('outputs/es-browser-fixtures/fixture.json', 'utf8'));
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } });
const page = await context.newPage();
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('dialog', dialog => dialog.accept());
try {
  await page.goto(`${fixture.url}/admin/clientes/${fixture.licenseA}`);
  assert(page.url().includes('/admin/login'), 'Anonymous customer details must require login.');
  await page.locator('[name=username]').fill(fixture.username);
  await page.locator('[name=password]').fill(fixture.password);
  await Promise.all([page.waitForURL(`${fixture.url}/admin`), page.locator('form button').click()]);
  await page.locator('#suite-clients .customer-row').first().waitFor();
  assert.equal(await page.locator('#suite-clients .customer-row').count(), 25, 'A page must be bounded to 25 customers.');
  assert.equal(await page.locator('#suite-clients img').count(), 0, 'Customer markup must be encoded.');
  const panelText = await page.locator('#suite-clients').innerText();
  assert(panelText.includes('TurboRama Suite') && panelText.includes('EmulationStation'));
  await page.screenshot({ path: 'outputs/es-panel-desktop.png', fullPage: true });
  const search = page.locator('#suite-clients [name=suiteSearch]');
  await search.fill('Cliente A');
  await page.waitForTimeout(16_000);
  assert.equal(await search.inputValue(), 'Cliente A', 'Polling must preserve a focused filter.');
  await Promise.all([page.waitForURL(/suiteSearch=/), page.locator('#suite-clients button').click()]);
  assert.equal(await page.locator('#suite-clients .customer-row').count(), 1);
  await page.goto(`${fixture.url}/admin?suitePage=2`);
  assert.equal(await page.locator('#suite-clients .customer-row').count(), 2, 'Pagination must reach the remaining clients.');
  await page.goto(`${fixture.url}/admin/suite?licenseId=${fixture.licenseA}`);
  assert((await page.locator('.suite-sessions').innerText()).includes('EmulationStation'), 'Existing Suite page must show the new sessions.');
  await page.goto(`${fixture.url}/admin/clientes/${fixture.licenseA}`);
  const section = page.locator('.suite-sessions');
  assert.equal(await section.locator('tbody tr').count(), 2, 'Suite and ES must be separate rows.');
  assert((await section.innerText()).includes('**:**:**:**:44:55'), 'The authenticated network report must appear masked.');
  assert(!(await section.innerText()).includes('02:11:22:33:44:55'), 'The panel must not reveal the full MAC.');
  const form = section.locator('form');
  const target = await form.locator('[name=target]').inputValue();
  const csrf = await form.locator('[name=__RequestVerificationToken]').inputValue();
  assert(!target.includes(fixture.sessionA), 'The browser confirmation must use a protected target.');
  const action = `${fixture.url}/admin/clientes/actions/revoke-es-session`;
  for (const fields of [
    { target, confirmTarget: '1', adminPassword: fixture.password },
    { target: `${target}tampered`, confirmTarget: '1', adminPassword: fixture.password, __RequestVerificationToken: csrf },
    { target, confirmTarget: '1', adminPassword: 'incorrect', __RequestVerificationToken: csrf },
    { target, adminPassword: fixture.password, __RequestVerificationToken: csrf }
  ]) {
    const denied = await context.request.post(action, { form: fields });
    assert(denied.url().includes('error='), 'CSRF, target, password and explicit confirmation must be checked.');
  }
  await page.reload();
  await section.locator('summary').click();
  await form.locator('[name=adminPassword]').fill(fixture.password);
  await page.waitForTimeout(16_000);
  assert(await section.locator('details').getAttribute('open') !== null, 'Polling must not close a confirmation.');
  assert.equal(await form.locator('[name=adminPassword]').inputValue(), fixture.password);
  await page.setViewportSize({ width: 390, height: 844 });
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Narrow layout must not overflow the page.');
  assert(await form.evaluate(element => { const box=element.getBoundingClientRect();return box.left>=0&&box.right<=innerWidth; }),
    'The session confirmation must fit the narrow viewport without horizontal scrolling.');
  await page.screenshot({ path: 'outputs/es-panel-narrow.png', fullPage: true });
  await form.locator('[name=confirmTarget]').check();
  await Promise.all([page.waitForURL(/ok=ES_SESSION_REVOKED/), form.locator('button').click()]);
  const revokedRows = page.locator('.suite-sessions tbody tr');
  assert((await revokedRows.filter({ hasText: 'EmulationStation' }).innerText()).includes('Revogada'));
  assert((await revokedRows.filter({ hasText: 'TurboRama Suite' }).innerText()).includes('Online'), 'Revoking ES must preserve Suite.');
  await page.goto(`${fixture.url}/admin/clientes/${fixture.licenseB}`);
  assert((await page.locator('.suite-sessions').innerText()).includes('Online'), 'Customer B must remain online.');
  assert.deepEqual(errors, [], 'Browser must not emit JavaScript errors.');
  const result = { result: 'passed', desktop: '1440x1000', narrow: '390x844', checks: ['real login', 'batch pagination', 'XSS encoding', 'Suite and ES rows', 'existing /admin/suite', 'focused filter', 'confirmation preservation', 'CSRF', 'protected target tamper', 'password step-up', 'explicit confirmation', 'exact ES revocation', 'Suite and B isolation'] };
  await fs.writeFile('outputs/es-panel-browser.json', JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally {
  await browser.close();
  await fs.writeFile('outputs/es-browser-fixtures/stop', 'done');
}
