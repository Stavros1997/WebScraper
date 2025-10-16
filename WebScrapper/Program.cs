using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumUndetectedChromeDriver;
using SeleniumExtras.WaitHelpers;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;

class Program
{
    static void Main()
    {
        List<string> MatchesWithPlayerProps = new List<string>();
        int PreviousPlayerPropsCount = -1;

        while (true)
        {
            ChromeDriver? driver = null;
            try
            {
                var options = new ChromeOptions();
                options.AddArgument("--headless");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-gpu");

                driver = new ChromeDriver(options);
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                var js = (IJavaScriptExecutor)driver;

                driver.Navigate().GoToUrl("https://www.betsson.gr/el/stoixima/basket/evrolinka/evrolinka?tab=liveAndUpcoming");
                Thread.Sleep(5000);

                Screenshot screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                screenshot.SaveAsFile("screenshot.png");
                try
                {
                    ClickOnPopUpAndCookies(driver, wait, js);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Popup or cookies click failed: {ex.Message}");
                    driver.Quit(); // close current browser
                    Thread.Sleep(2000);

                    // Start a fresh browser instance and retry the loop
                    continue;
                }
                var matTypography = GetLastShadowDOM(wait);



                ScrollDown(matTypography, js);
                var elements = matTypography.FindElements(By.CssSelector(".obg-event-scorecard-labels.event-table.ng-star-inserted"));
                if(elements.Count == 0)
                {
                    Console.WriteLine("❌ No elements found, restarting...");
                    try { driver?.Quit(); } catch { /* ignore */ }
                    Thread.Sleep(2000);
                    continue;
                }
                List<string> teamNames = new List<string>();
                try { 
                    teamNames = elements.Select(e => e.Text).ToList();
                }
                catch
                {
                    driver.Navigate().Refresh();
                    Thread.Sleep(10000);
                    matTypography = GetLastShadowDOM(wait);
                    ScrollDown(matTypography, js);
                    elements = matTypography.FindElements(By.CssSelector(".obg-event-scorecard-labels.event-table.ng-star-inserted"));
                    teamNames = elements.Select(e => e.Text).ToList();

                }
                for (int i = 0; i < teamNames.Count; i++)
                {
                    try
                    {
                        string Teams = teamNames[i];
                        elements[i].Click();
                        Thread.Sleep(1000);
                        try
                        {
                            bool exists = matTypography.FindElements(By.CssSelector("span.ng-star-inserted")).Any(e => e.Text.Trim() == "Αγορές παικτών");
                            if (exists)
                                MatchesWithPlayerProps.Add(Teams);
                        }
                        catch
                        {
                            Console.WriteLine("❌ Element not found.");
                        }

                        driver.Navigate().Back();
                        Thread.Sleep(1000);
                        ScrollDown(matTypography, js);
                        matTypography = GetLastShadowDOM(wait);
                        elements = matTypography.FindElements(By.CssSelector(".obg-event-scorecard-labels.event-table.ng-star-inserted"));
                        while (elements.Count != teamNames.Count)
                        {
                            driver.Navigate().Refresh();
                            Thread.Sleep(10000);
                            matTypography = GetLastShadowDOM(wait);
                            ScrollDown(matTypography, js);
                            elements = matTypography.FindElements(By.CssSelector(".obg-event-scorecard-labels.event-table.ng-star-inserted"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception {ex}, occured at {i}, refetching and retrying...");
                        matTypography = GetLastShadowDOM(wait);
                        elements = matTypography.FindElements(By.CssSelector(".obg-event-scorecard-labels.event-table.ng-star-inserted"));
                        while (elements.Count != teamNames.Count)
                        {
                            driver.Navigate().Refresh();
                            Thread.Sleep(10000);
                            matTypography = GetLastShadowDOM(wait);
                            ScrollDown(matTypography, js);
                            elements = matTypography.FindElements(By.CssSelector(".obg-event-scorecard-labels.event-table.ng-star-inserted"));
                        }
                        i--; // retry same index
                    }

                    

                }
                if (PreviousPlayerPropsCount == MatchesWithPlayerProps.Count)
                {
                    //do not send an email if the count is the same as last time

                }
                else
                {
                    PreviousPlayerPropsCount = MatchesWithPlayerProps.Count;
                    SendEmail(MatchesWithPlayerProps);
                    

                }
                MatchesWithPlayerProps.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}. Restarting driver...");
                try { driver?.Quit(); } catch { /* ignore */ }
                Thread.Sleep(2000);
                continue; // restart the loop
            }
            finally
            {
                // Always close driver safely
                try { driver?.Quit(); } catch { /* ignore */ }
            }

            Console.WriteLine("⏳ Waiting 5 minutes before next run...");
            //Thread.Sleep(300000); // wait 5 minutes
        }
    }

    public static void ClickOnPopUpAndCookies(ChromeDriver driver,WebDriverWait? wait,IJavaScriptExecutor js)
    {
        var acceptButton = driver.FindElement(By.Id("onetrust-accept-btn-handler"));
        acceptButton.Click();

        

        // Wait for and click the button inside nested shadow roots
        var PopUpButton = wait.Until(d =>
        {
            var result = (IWebElement?)js.ExecuteScript(@"
                const root1 = document.querySelector('site-root_default');
                if (!root1) return null;
                const shadow1 = root1.shadowRoot;
                if (!shadow1) return null;

                const root2 = shadow1.querySelector('site-cta_modal');
                if (!root2) return null;
                const shadow2 = root2.shadowRoot;
                if (!shadow2) return null;

                return shadow2.querySelector('button.site-cta_modal-button-close');
            ");
            return result;
        });
        // Use JS to click the button (works reliably inside shadow DOM)
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", PopUpButton);
    }

    public static void ScrollDown(IWebElement? matTypography, IJavaScriptExecutor js)
    {
        for (int a = 0; a < 3; a++)
        {
            js.ExecuteScript(@"
        const root = arguments[0];
        const contents = root.querySelectorAll('.obg-scrollbar-content');
        if (contents.length > 1) {
            const second = contents[1];
            second.scrollTop += 400; // scroll by 400px
        }
    ", matTypography);

            Thread.Sleep(3000); // wait for lazy content to load
        }
    }

    public static IWebElement GetLastShadowDOM(WebDriverWait? wait)
    {
        var router = wait.Until(d => d.FindElement(By.CssSelector("site-root_default")));
        var shadowRouter = router.GetShadowRoot();

        //var dialog = shadowRouter.FindElement(By.CssSelector("fds-dialog"));
        //var shadowDialog = router.GetShadowRoot();

        var layout = shadowRouter.FindElement(By.CssSelector("router-fabric_outlet"));
        var shadowLayout = layout.GetShadowRoot();

        //var sportsbookLayout = shadowLayout.FindElement(By.CssSelector("gaming-sportsbook_layout"));
        //var shadowSportsbook = sportsbookLayout.GetShadowRoot();

        var switcher = shadowLayout.FindElement(By.CssSelector("gaming-sportsbook_switcher"));
        var shadowSwitcher = switcher.GetShadowRoot();

        //var mfe = shadowSwitcher.FindElement(By.CssSelector("gaming-sportsbook-mfe"));
        //var shadowMfe = mfe.GetShadowRoot();

        var app = shadowSwitcher.FindElement(By.CssSelector("sb-xp-sportsbook-app"));
        var shadowApp = app.GetShadowRoot();

        // now you’re inside the last shadow DOM
        var matTypography = shadowApp.FindElement(By.CssSelector("div.mat-typography"));
        return matTypography;

    }

    public static void SendEmail(List<string> matches)
    {
        Environment.GetEnvironmentVariable("EMAIL_FROM_USER");
        var cleanedMatches = matches
            .Select(m => m.Replace("\r", " ").Replace("\n", " ").Trim())
            .Where(m => !string.IsNullOrEmpty(m))
            .ToList();

        string body = "Matches with Player Props:\n\n" + string.Join("\n", cleanedMatches);

        // Gmail sender credentials
        string fromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM_USER");
        string fromPassword = Environment.GetEnvironmentVariable("EMAIL_PASS");
        string toAddress = Environment.GetEnvironmentVariable("EMAIL_TO_USER");

        // 📬 Multiple recipients
        List<string> toAddresses = new List<string>
    {
        fromAddress,
        toAddress
    };

        try
        {
            using (var client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(fromAddress, fromPassword);

                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(fromAddress);
                    foreach (var to in toAddresses)
                        mail.To.Add(to);

                    mail.Subject = "Matches with Player Props";
                    mail.Body = body;
                    mail.BodyEncoding = Encoding.UTF8;
                    mail.SubjectEncoding = Encoding.UTF8;

                    client.Send(mail);
                }
            }

            Console.WriteLine("✅ Email sent successfully to: " + string.Join(", ", toAddresses));
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Failed to send email: " + ex.Message);
        }
    }
}
