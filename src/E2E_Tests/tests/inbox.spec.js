// @ts-check
const { test, expect } = require('@playwright/test');
const { login, CREDENTIALS } = require('../helpers/auth');

async function contacts(page) {
  const res = await page.request.get('/api/Messages/userContacts');
  return JSON.parse(await res.text()).contacts || [];
}
function findContact(list, name) {
  return list.find((c) => new RegExp(name, 'i').test(c.name || '') || new RegExp(name, 'i').test(c.nickname || ''));
}

test.describe('B8: inbox unread state + message body integrity', () => {
  test('a sent message is unread, marked read on open, and preserves & / # characters', async ({ browser }) => {
    // Sender (DemoPlayer)
    const ctxA = await browser.newContext({ ignoreHTTPSErrors: true });
    const pageA = await ctxA.newPage();
    await login(pageA, CREDENTIALS.user);
    await pageA.goto('/');
    const t2 = findContact(await contacts(pageA), 'TestUser2');
    expect(t2, 'DemoPlayer should have TestUser2 as a seeded contact').toBeTruthy();

    const tokenA = await pageA.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const body = 'hello & world #ranked';
    const send = await pageA.request.post('/api/Messages/sendMessageTo', {
      headers: { RequestVerificationToken: tokenA, 'Content-Type': 'application/json' },
      data: { toId: t2.id, message: body },
    });
    expect(send.status(), 'message should send').toBe(200);

    // Recipient (TestUser2)
    const ctxB = await browser.newContext({ ignoreHTTPSErrors: true });
    const pageB = await ctxB.newPage();
    await login(pageB, CREDENTIALS.user2);
    await pageB.goto('/');

    const unreadBefore = await (await pageB.request.get('/api/Messages/messageCount')).json();
    expect(unreadBefore, 'recipient should have at least one unread message').toBeGreaterThanOrEqual(1);

    const demoPlayer = findContact(await contacts(pageB), 'DemoPlayer');
    expect(demoPlayer, 'TestUser2 should have DemoPlayer as a contact').toBeTruthy();

    // Opening the conversation returns the messages (with & and # intact) and marks them read.
    const betweenRaw = await (await pageB.request.get(`/api/Messages/messagesBetween?toId=${demoPlayer.id}`)).text();
    expect(betweenRaw, 'message body should survive & and #').toContain(body);

    const unreadAfter = await (await pageB.request.get('/api/Messages/messageCount')).json();
    expect(unreadAfter, 'unread count should drop after opening the conversation').toBeLessThan(unreadBefore);

    await ctxA.close();
    await ctxB.close();
  });

  test('the in-browser messaging UI actually loads a conversation thread (Messages.js contract)', async ({ browser }) => {
    // This drives the real client JS (not the API directly), which is where the read contract was broken:
    // Messages.js used to call messagesBetween with the caller's OWN id as toId -> GetMessagesBetween(self,self).
    const body = 'ui-thread-check ' + Date.now();

    // Arrange: DemoPlayer sends TestUser2 a message.
    const ctxA = await browser.newContext({ ignoreHTTPSErrors: true });
    const pageA = await ctxA.newPage();
    await login(pageA, CREDENTIALS.user);
    await pageA.goto('/');
    const t2 = findContact(await contacts(pageA), 'TestUser2');
    expect(t2, 'DemoPlayer should have TestUser2 as a seeded contact').toBeTruthy();
    const tokenA = await pageA.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const send = await pageA.request.post('/api/Messages/sendMessageTo', {
      headers: { RequestVerificationToken: tokenA, 'Content-Type': 'application/json' },
      data: { toId: t2.id, message: body },
    });
    expect(send.status()).toBe(200);

    // Act: TestUser2 opens the messaging panel UI and clicks the DemoPlayer contact.
    const ctxB = await browser.newContext({ ignoreHTTPSErrors: true });
    const pageB = await ctxB.newPage();
    await login(pageB, CREDENTIALS.user2);
    await pageB.goto('/');
    const demoPlayer = findContact(await contacts(pageB), 'DemoPlayer');
    expect(demoPlayer, 'TestUser2 should have DemoPlayer as a contact').toBeTruthy();

    await pageB.locator('.messages-btn').click();
    const contactTile = pageB.locator(`.contact[id="${demoPlayer.id}"]`);
    await contactTile.waitFor({ state: 'visible' });
    await contactTile.click();

    // Assert: the conversation pane renders the message the buggy contract could never have fetched.
    await expect(pageB.locator('.messages-for-contact')).toContainText(body, { timeout: 5000 });

    await ctxA.close();
    await ctxB.close();
  });
});
