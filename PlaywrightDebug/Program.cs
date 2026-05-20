using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

class Program
{
    static async Task<int> Main()
    {
        var baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5127";
        Console.WriteLine($"DEBUG: BaseUrl={baseUrl}");

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        page.Console += (_, msg) => Console.WriteLine($"CONSOLE [{msg.Type}] {msg.Text}");
        page.RequestFailed += (_, req) => Console.WriteLine($"REQ_FAIL {req.Url} {req.Failure}");
        page.Request += (_, req) => Console.WriteLine($"REQUEST {req.Method} {req.Url}");
        page.Response += async (_, res) => Console.WriteLine($"RESPONSE {res.Url} {res.Status} {res.Headers.GetValueOrDefault("content-type", "")}");

        Console.WriteLine("Navigating...");
        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
        Console.WriteLine("Waiting 3s for Blazor startup...");
        await page.WaitForTimeoutAsync(3000);
        Console.WriteLine("Capturing content...");
        var content = await page.ContentAsync();
        Console.WriteLine("PAGE_CONTENT_START");
        Console.WriteLine(content.Length > 20000 ? content.Substring(0, 20000) : content);
        Console.WriteLine("PAGE_CONTENT_END");

        var locator = page.Locator("text=Official sign-in");
        var visible = await locator.IsVisibleAsync();
        Console.WriteLine($"Locator visible: {visible}");

        if (visible)
        {
            Console.WriteLine("Attempting sign-in for Mark Phillipson");
            await page.SelectOptionAsync(".modal-backdrop select", new[] { new Microsoft.Playwright.SelectOptionValue { Label = "Mark Phillipson" } });
            await page.FillAsync(".modal-backdrop input[type='password']", "2468");
            await page.ClickAsync(".modal-backdrop button.primary-action", new PageClickOptions { Force = true });
            Console.WriteLine("Clicked Sign in — waiting for modal to hide...");
            try
            {
                await page.WaitForSelectorAsync(".modal-backdrop", new() { State = Microsoft.Playwright.WaitForSelectorState.Hidden, Timeout = 15000 });
                Console.WriteLine("Modal hidden after sign-in.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Modal did not hide: {ex.Message}");
            }

            // Give the client a moment to update the UI
            await page.WaitForTimeoutAsync(1000);
            var after = await page.ContentAsync();
            Console.WriteLine("PAGE_CONTENT_AFTER_START");
            Console.WriteLine(after.Length > 20000 ? after.Substring(0, 20000) : after);
            Console.WriteLine("PAGE_CONTENT_AFTER_END");
        }

        await page.ScreenshotAsync(new PageScreenshotOptions { Path = "debug-screenshot.png", FullPage = true });
        Console.WriteLine("Saved screenshot to debug-screenshot.png");

        await browser.CloseAsync();
        return visible ? 0 : 1;
    }
}
