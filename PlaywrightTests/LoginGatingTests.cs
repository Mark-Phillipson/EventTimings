using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace PlaywrightTests
{
    public class LoginGatingTests : PageTest
    {
        private string BaseUrl => Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "https://localhost:7041";

        [Test]
        public async Task LoginModalIsShown()
        {
            // Attach simple diagnostics
            Page.Console += (_, msg) => TestContext.WriteLine($"BROWSER_CONSOLE [{msg.Type}] {msg.Text}");
            Page.RequestFailed += (_, req) => TestContext.WriteLine($"REQUEST_FAILED {req.Url} {req.Failure}");

            // Clear any service workers / caches from previous runs to avoid SRI/cache mismatches
            await Page.GotoAsync("about:blank");
            await Page.EvaluateAsync(@"() => {
                try {
                    if (typeof navigator !== 'undefined' && navigator.serviceWorker && navigator.serviceWorker.getRegistrations) {
                        return navigator.serviceWorker.getRegistrations().then(regs => Promise.all(regs.map(r => r.unregister())));
                    }
                } catch (e) { /* ignore */ }
                return Promise.resolve();
            }");
            await Page.EvaluateAsync(@"() => {
                try {
                    if (typeof caches !== 'undefined' && caches.keys) {
                        return caches.keys().then(keys => Promise.all(keys.map(k => caches.delete(k))));
                    }
                } catch (e) { /* ignore */ }
                return Promise.resolve();
            }");

            await Page.GotoAsync(BaseUrl);
            // Debug: dump some page content to the test output to help diagnose why modal isn't visible
            var content = await Page.ContentAsync();
            TestContext.WriteLine("PAGE_CONTENT_START");
            TestContext.WriteLine(content.Length > 8000 ? content.Substring(0, 8000) : content);
            TestContext.WriteLine("PAGE_CONTENT_END");

            // Wait up to 10s for the login modal to render (Blazor WebAssembly startup can be slow)
            var header = await Page.WaitForSelectorAsync("text=Official sign-in", new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 30000 });
            Assert.That(header, Is.Not.Null, "Login modal header should be present after app startup.");
        }

        [Test]
        public async Task SignInEnablesControls()
        {
            // Diagnostics
            Page.Console += (_, msg) => TestContext.WriteLine($"BROWSER_CONSOLE [{msg.Type}] {msg.Text}");
            Page.RequestFailed += (_, req) => TestContext.WriteLine($"REQUEST_FAILED {req.Url} {req.Failure}");

            // Clear service workers / caches to avoid stale assets causing SRI errors
            await Page.GotoAsync("about:blank");
            await Page.EvaluateAsync(@"() => {
                try {
                    if (typeof navigator !== 'undefined' && navigator.serviceWorker && navigator.serviceWorker.getRegistrations) {
                        return navigator.serviceWorker.getRegistrations().then(regs => Promise.all(regs.map(r => r.unregister())));
                    }
                } catch (e) { /* ignore */ }
                return Promise.resolve();
            }");
            await Page.EvaluateAsync(@"() => {
                try {
                    if (typeof caches !== 'undefined' && caches.keys) {
                        return caches.keys().then(keys => Promise.all(keys.map(k => caches.delete(k))));
                    }
                } catch (e) { /* ignore */ }
                return Promise.resolve();
            }");

            await Page.GotoAsync(BaseUrl);
            // Wait for modal
            var header = await Page.WaitForSelectorAsync("text=Official sign-in", new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 30000 });
            Assert.That(header, Is.Not.Null, "Login modal should appear before sign-in.");

            // Select official and enter PIN (seeded in the API)
            await Page.SelectOptionAsync(".modal-backdrop select", new[] { new Microsoft.Playwright.SelectOptionValue { Label = "Mark Phillipson" } });
            await Page.FillAsync(".modal-backdrop input[type='password']", "2468");

            var signInBtn = Page.Locator(".modal-backdrop button.primary-action");
            await signInBtn.WaitForAsync(new Microsoft.Playwright.LocatorWaitForOptions { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });

            // Start waiting for the verify API response so we can assert it completed successfully
            var verifyResponseTask = Page.WaitForResponseAsync(
                resp => resp.Url.Contains("/api/admin/officials/verify") && resp.Request.Method == "POST",
                new Microsoft.Playwright.PageWaitForResponseOptions { Timeout = 30000 }
            );

            // Use a forced click in case a backdrop or animation briefly intercepts pointer events
            await signInBtn.ClickAsync(new Microsoft.Playwright.LocatorClickOptions { Force = true });

            // Ensure the verify POST completed
            Microsoft.Playwright.IResponse verifyResp = null;
            try
            {
                verifyResp = await verifyResponseTask;
                TestContext.WriteLine($"Verify response status: {verifyResp.Status}");
                var verifyBody = await verifyResp.TextAsync();
                TestContext.WriteLine(verifyBody.Length > 16000 ? verifyBody.Substring(0, 16000) : verifyBody);

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(verifyBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("success", out var successProp))
                    {
                        Assert.That(successProp.GetBoolean(), Is.True, "API verify returned success=false");
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // ignore — assertion below will still check modal hid
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Verify response wait failed: {ex.GetType().Name} {ex.Message}");
                // Dump some diagnostics
                var dump = await Page.ContentAsync();
                TestContext.WriteLine(dump.Length > 16000 ? dump.Substring(0, 16000) : dump);
                throw;
            }

            // Wait for modal to hide (allow more time for UI update)
            try
            {
                await Page.WaitForSelectorAsync(".modal-backdrop", new() { State = Microsoft.Playwright.WaitForSelectorState.Hidden, Timeout = 30000 });
            }
            catch (TimeoutException)
            {
                TestContext.WriteLine("Modal did not hide in time — dumping page content for diagnostics.");
                var dump = await Page.ContentAsync();
                TestContext.WriteLine(dump.Length > 16000 ? dump.Substring(0, 16000) : dump);
                throw;
            }

            // Start button should become enabled
            var startBtn = Page.Locator("button:has-text('Start')");
            await startBtn.WaitForAsync(new Microsoft.Playwright.LocatorWaitForOptions { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });
            Assert.That(await startBtn.IsDisabledAsync(), Is.False, "Start button should be enabled after successful sign-in.");

            // Officials nav link should now be visible
            var officialsNav = Page.Locator("nav a:has-text('Officials')");
            await officialsNav.WaitForAsync(new Microsoft.Playwright.LocatorWaitForOptions { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });
            Assert.That(await officialsNav.IsVisibleAsync(), Is.True, "Officials nav link should be visible after sign-in.");
        }
    }
}
