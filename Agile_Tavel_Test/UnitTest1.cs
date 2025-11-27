
using Microsoft.Playwright;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Agile_Tavel_Test
{
    // [TestFixture] identifies this class as a container for NUnit tests
    [TestFixture]
    internal class FlightBookingSuite
    {
        // Private fields to hold the Playwright objects
        private IPlaywright _playwright;
        private IBrowser _browser;
        private IPage _page;

        // --- NUNIT SETUP: Runs before each test method ---
        [SetUp]
        public async Task Setup()
        {
            // Manual Playwright setup
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
            _page = await _browser.NewPageAsync();

            await _page.SetViewportSizeAsync(1920, 1080);
            await _page.GotoAsync("https://travel.agileway.net/login");
        }

        // --- 1. POSITIVE E2E TEST: Complete Booking Flow ---
        [Test]
        [Category("E2E")]
        public async Task Test_01_Complete_Flight_Booking_E2E()
        {
            // --- 1. Login ---
            await _page.Locator("#username").FillAsync("agileway");
            await _page.Locator("#password").FillAsync("test$W1se");
            await _page.Locator("[type='checkbox']").ClickAsync();
            await _page.Locator("input[value='Sign in']").ClickAsync();

            await _page.WaitForURLAsync("https://travel.agileway.net/flights/start");

            // --- 2. Search Flight (Cleanup and robust selectors applied) ---

            // Note: You clicked both, so keeping the last one (Roundtrip)
            await _page.Locator("input[value='return']").ClickAsync();

            await _page.SelectOptionAsync("select[name='fromPort']", new[] { "New York" });
            await _page.SelectOptionAsync("select[name='toPort']", new[] { "Sydney" });

            await _page.SelectOptionAsync("#departDay", "05");
            await _page.SelectOptionAsync("#departMonth", "June 2025");
            await _page.SelectOptionAsync("#returnDay", "25");
            await _page.SelectOptionAsync("#returnMonth", "March 2026");

            await _page.Locator("input[value='Continue']").ClickAsync(); // Submit Search

            // ASSERTION 1: Verify navigation to the Select Flight page
            await _page.WaitForURLAsync("");
            //Assert.That(await _page.TitleAsync(), Does.Contain("Select Flight"), "Did not reach the flight selection page.");

            //// --- 3. Select Flight and Passenger Details ---

            //// Click the first radio button to select the flight (Using a better selector)
            //await _page.Locator("#flights input[type='radio']").First.ClickAsync();
            //await _page.Locator("input[value='Continue']").ClickAsync();

            //// ASSERTION 2: Verify navigation to the Passenger Details page
            // await _page.WaitForURLAsync("");

            // Fill Passenger Details
            await _page.Locator("input[name='passengerFirstName']").FillAsync("Nami");
            await _page.Locator("input[name='passengerLastName']").FillAsync("Element");
            await _page.Locator("input[value='Next']").ClickAsync();

            // --- 4. Payment ---

            // ASSERTION 3: Verify navigation to the Payment page
            await _page.WaitForURLAsync("");

            // Select Card Type (Visa)
            await _page.Locator("#payment-form input[value='visa']").ClickAsync();

            // Fill Card Number
            await _page.Locator("input[name='card_number']").FillAsync("5210458520256");

            // Select Expiration Date
            await _page.Locator("select[name='expiry_month']").SelectOptionAsync("03");
            await _page.Locator("select[name='expiry_year']").SelectOptionAsync("2027");

            await _page.Locator("input[value='Pay now']").ClickAsync();

            // --- 5. Confirmation and Logout ---

            // ASSERTION 4: Verify navigation to the Confirmation page
            await _page.WaitForURLAsync("");
            
            Assert.That(await _page.Locator("#confirmation > h2").TextContentAsync(), Does.Contain("Confirmation"), "Booking confirmation message not found.");

            // Logout
            await _page.Locator("#user_nav a[href='/logout']").ClickAsync();
            await _page.WaitForURLAsync("");
            Assert.That(_page.Url, Does.Contain("/login"), "Failed to logout and return to login page.");
        }

        // --- 2. NEGATIVE TEST: Invalid Login Attempt (from previous response) ---
        [Test]
        [Category("Negative")]
        public async Task Test_02_Invalid_Login_Fails()
        {
            // ... (Your previous invalid login logic) ...
            await _page.Locator("#username").FillAsync("wronguser");
            await _page.Locator("#password").FillAsync("badpass");
            await _page.Locator("input[value='Sign in']").ClickAsync();
            //await Task.Delay(10_000);
            await _page.WaitForSelectorAsync("#flash_alert", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });

            var errorMessage = await _page.Locator("#flash_alert").TextContentAsync();

            Assert.Multiple(() =>
            {
                Assert.That(errorMessage, Does.Contain("Invalid email or password"), "Expected error message not found.");
                Assert.That(_page.Url, Is.EqualTo("https://travel.agileway.net/sessions"), "URL changed, indicating an incorrect redirect on failure.");
            });
        }

        // --- 3. NEGATIVE TEST: Missing Required Search Data ---
        [Test]
        [Category("Negative")]
        public async Task Test_03_Search_Fails_Without_Destination()
        {
            // Login is required to access the flights page
            await _page.Locator("#username").FillAsync("agileway");
            await _page.Locator("#password").FillAsync("test$W1se");
            await _page.Locator("input[value='Sign in']").ClickAsync();
            await _page.WaitForURLAsync("");

            // 1. Enter all data EXCEPT the destination port (toPort)
            await _page.Locator("input[value='return']").ClickAsync();

            await _page.SelectOptionAsync("select[name='fromPort']", new[] { "New York" });
            // *** OMITTING: await _page.SelectOptionAsync("select[name='toPort']", new[] { "Sydney" }); ***

            await _page.SelectOptionAsync("#departDay", "05");
            await _page.SelectOptionAsync("#departMonth", "June 2025");

            // 2. Submit the Search
            await _page.Locator("input[value='Continue']").ClickAsync();

            // ASSERTION: Verify the validation error is displayed and the page remains the same.
            // When a required field is empty, the browser typically shows a validation bubble.
            // On this site, it seems to reload the page or stay on the current URL.

            // We expect the URL to remain unchanged and no new "Select Flight" page to load.
            Assert.That(_page.Url, Is.SupersetOf("https://travel.agileway.net/flights"),
                "Navigation occurred despite missing required field.");

            // Check for a specific element that might show an error, or simply confirm
            // we did not navigate to the next screen (Title verification is often the best check).
            Assert.That(await _page.TitleAsync(), Does.Not.Contain("Select Flight"),
                "Incorrectly proceeded to the next stage of booking.");
        }

        // --- NUNIT TEARDOWN: Runs after each test method ---
        [TearDown]
        public async Task Teardown()
        {
            // Clean up resources
            if (_browser != null)
            {
                await _browser.CloseAsync();
            }
            if (_playwright != null)
            {
                _playwright.Dispose();
            }
        }
    }
}
