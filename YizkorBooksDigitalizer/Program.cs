﻿
using System.Net;
using System.Text.RegularExpressions;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text;
using Google.Cloud.Vision.V1;

//DownloadBook("https://digitalcollections.nypl.org/items/73b73820-541b-0133-dbe2-00505686d14e", "novoselitza");
DigitalizeYizkorBook("novoselitza");

#region Selenium Scraping
void DownloadBook(string uri, string placeName = null, bool headless = false)
{
    var chromeOptions = new ChromeOptions { BinaryLocation = Path.Combine(Environment.CurrentDirectory, "chromedriver.exe") };
    if (headless)
    {
        //does not work well (does not complete "next click" steps)
        chromeOptions.AddArguments("headless");
    }

    var wd = new ChromeDriver(chromeOptions);
    wd.Navigate().GoToUrl(uri);
    //await Task.Delay(TimeSpan.FromMilliseconds(250)); //causes flow to return to caller, which will start a new DownloadFullBook call on the next book !
    //I prefer to block the current thread and keep a single thread only
    Thread.Sleep(TimeSpan.FromMilliseconds(250));

    Console.Clear();
    //$("#results-list a").attr('href')

    if (string.IsNullOrEmpty(placeName))
    {
        var placeNameElement = wd.FindElement(By.CssSelector(".item-left h1"));
        if (placeNameElement == null) return;

        placeName = placeNameElement.Text;
    }

    placeName = placeName.Replace(":", string.Empty);

    DirectoryInfo dir = null;
    if (!Directory.Exists(placeName))
        dir = Directory.CreateDirectory(placeName);
    else
        dir = new DirectoryInfo(placeName);

    //await Task.Delay(TimeSpan.FromMilliseconds(250));
    Thread.Sleep(TimeSpan.FromMilliseconds(250));

    #region deprecated number of pages approach
    /* this method is not reliable (because the NYPL drop down shows a wrong number of pages in the "load next pages" text - so I download many other images in addition to the full book, I should choose something else */

    //var viewAsBookElement = wd.FindElement(By.CssSelector("#actions .book a"));
    //if (viewAsBookElement == null) return;

    //var viewAsBookUri = viewAsBookElement.GetAttribute("href");
    //wd.Navigate().GoToUrl(viewAsBookUri);

    //var jumpToSelectOptions = wd.FindElements(By.CssSelector("#jump-to-select option"));
    //if (jumpToSelectOptions == null) return;

    //var regex = new Regex(@"\d+[,\.]?\d+\s+pages");
    //var lastOptionTitle = jumpToSelectOptions.LastOrDefault().GetAttribute("title");
    //var match = regex.Match(lastOptionTitle);
    //if (match == null) return;

    //var totalPagesNumStr = match.Value.Replace("pages", string.Empty).Replace(",", string.Empty);
    //int.TryParse(totalPagesNumStr, out int pagesNum);

    ////https://digitalcollections.nypl.org/items/7086f2d0-7a6d-0133-06d1-00505686a51c
    ////https://digitalcollections.nypl.org/items/7086f2d0-7a6d-0133-06d1-00505686a51c/book#page/1/mode/1up 
    #endregion

    var regexId = new Regex(@"id=\d+");


    var morePagesLeft = true;
    int lastImgId = 0;
    int numOfClicksOnNext = 0;
    do
    {
        var nextButton = wd.FindElement(By.CssSelector(".carousel-widget-wrapper .next_page"));
        Thread.Sleep(TimeSpan.FromMilliseconds(100));
        if (nextButton == null) return;

        var classAttribute = nextButton.GetAttribute("class");
        morePagesLeft = !classAttribute.Contains("disabled");

        if (morePagesLeft)
        {
            //nextButton.Click();
            var actions = new Actions(wd);
            actions.MoveToElement(nextButton).Click().Perform();
            numOfClicksOnNext++;

            Console.Clear();
            Console.WriteLine($"next click: {numOfClicksOnNext}");
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }
        else
        {
            var lis = wd.FindElements(By.CssSelector(".carousel-widget-list li"));
            var lastLiElement = lis?.LastOrDefault();
            if (lastLiElement == null) return;

            var outers = lis.Select(e => e.GetAttribute("outerHTML")).ToList();

            var lastImgUrl = lis.Where(l => !string.IsNullOrEmpty(l.GetAttribute("title")))?.Last()?.FindElement(By.CssSelector("img"))?.GetAttribute("src");

            //var inners = lis.Select(e => e.GetAttribute("innerHTML")).ToList();
            //var lastImgUrl = lastLiElement.FindElement(By.CssSelector("img"))?.GetAttribute("src");


            var match = regexId.Match(lastImgUrl);
            if (match == null) return;
            var idOnlyStr = match?.Value.Replace("id=", string.Empty);

            int.TryParse(idOnlyStr, out lastImgId);
        }

    } while (morePagesLeft);

    Console.Clear();
    Console.WriteLine($"Last img id: {lastImgId}");



    //get 1st page, and then extract id and increment by 1 for each subsequent pages
    wd.Navigate().GoToUrl($"{uri}/book#page/1/mode/1up");
    //await Task.Delay(TimeSpan.FromMilliseconds(250));
    Thread.Sleep(TimeSpan.FromMilliseconds(250));

    var imgSrcElement = wd.FindElement(By.CssSelector($"#pagediv1 img"));
    if (imgSrcElement == null) return;
    var imgSrc = imgSrcElement.GetAttribute("src");

    //var queryStringStartingPos = imgSrc.IndexOf('?');
    //var uriObj = new Uri(imgSrc);
    //var query = uriObj.Query;
    //var parsedQs = HttpUtility.ParseQueryString(query);
    //long.TryParse(parsedQs["id"], out long id);

    //#region extract metadata



    //var metadataElement = wd.FindElementById("item-content-data");
    //var nyplId = string.Empty;
    //if (metadataElement != null)
    //{
    //    var links = metadataElement.FindElements(By.TagName("a")).Where(a => a.GetAttribute("href").Contains("http://catalog.nypl.org/record="));
    //    var link = links.FirstOrDefault();
    //    if (link != null)
    //    {
    //        nyplId = link.Text.Replace("http://catalog.nypl.org/record=", string.Empty);
    //        wd.Navigate().GoToUrl($"http://catalog.nypl.org/record={nyplId}");

    //        var headers = wd.FindElements(By.CssSelector(".bibInfoLabel"))?.Select(e => e.Text);

    //    }
    //}

    //#endregion

    #region Get Country using Google Places Detail API

    var country = string.Empty;
    var language = string.Empty;
    using (var httpClient = new HttpClient())
    {
        var res = httpClient.GetStringAsync($"https://maps.googleapis.com/maps/api/place/findplacefromtext/json?input={placeName}&inputtype=textquery&key=AIzaSyBAmz3I0KOOxz5hvr2WdARyT0S-IGeSQ-w").Result;
        //var googlePlacesObj = JsonConvert.DeserializeObject<GooglePlaces>(res);
        //var placeId = googlePlacesObj?.Candidates?.FirstOrDefault()?.place_id ?? string.Empty;

        //https://www.newtonsoft.com/json/help/html/SelectToken.htm
        var placeId = (string)JObject.Parse(res)?.SelectToken("candidates[0].place_id");

        var detailCallRes = httpClient.GetStringAsync($"https://maps.googleapis.com/maps/api/place/details/json?key=AIzaSyBAmz3I0KOOxz5hvr2WdARyT0S-IGeSQ-w&place_id={placeId}").Result;
        country = (string)JObject.Parse(detailCallRes)?.SelectToken("result.address_components[3].long_name");

        //WORKING !
    }
    #endregion

    wd.Quit();

    var imgUrl = imgSrc;
    var webClient = new WebClient();

    //var currentImgId = 0;
    var matchValue = regexId.Match(imgUrl)?.Value;
    var idOnly = matchValue.Replace("id=", string.Empty);
    int.TryParse(idOnly, out int currentImgId);

    var totalPages = lastImgId - currentImgId + 1;
    var page = 0;
    do
    {
        //for (int page = 1; page < numOfPages; page++)
        //{

        //wd.Navigate().GoToUrl($"{uri}/book#page/{page}/mode/1up");
        //await Task.Delay(TimeSpan.FromMilliseconds(250));

        //var imgSrcElement = wd.FindElement(By.CssSelector($"#pagediv{page} img"));
        //if (imgSrcElement == null) continue;
        //var imgSrc = imgSrcElement.GetAttribute("src");

        webClient.DownloadFile(imgUrl, $"{dir.FullName}/{placeName} page {++page}.jpg");
        //Thread.Sleep(TimeSpan.FromSeconds(0.5));
        Thread.Sleep(TimeSpan.FromSeconds(0.15));

        //await Task.Delay(TimeSpan.FromSeconds(1));
        imgUrl = regexId.Replace(imgUrl, $"id={++currentImgId}");

        Console.Clear();
        Console.WriteLine($"{placeName}: {page}/{totalPages}");
        //}
    }
    while (currentImgId <= lastImgId);

    webClient.Dispose();

    //write metadata json
    File.WriteAllText($"{dir.FullName}/{placeName} metadata.json", JsonConvert.SerializeObject(new { country, language }, Newtonsoft.Json.Formatting.Indented));
    Directory.Move(dir.FullName, $@"C:\Users\avraham.kahana\Downloads\books\{placeName}");
}

void DownloadBooks()
{
    //var skipBooks = 18;
    var booksFolder = $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "books")}";
    if (!Directory.Exists(booksFolder))
    {
        Directory.CreateDirectory(booksFolder);
    }
    var skipBooks = Directory.GetDirectories(booksFolder).Length;
    var books = File.ReadAllLines("book list NYPL PAGE 2 of 5.csv", Encoding.UTF8).Skip(1 + skipBooks); //skip header line
    var limit = 3;
    List<Task> tasks = new List<Task>();


    Console.WriteLine("looping...");
    //foreach (var book in books.Skip(skipBooks))
    for (int i = 0; i < books.Count() && i < limit; i++)
    {
        var book = books.ElementAt(i);
        var task = Task.Run(() =>
        {
            var fields = book.Split(new string[] { "\",\"" }, StringSplitOptions.None);
            var name = fields.FirstOrDefault().Trim(new char[] { '"' });
            var link = fields.LastOrDefault().Trim(new char[] { '"' });

            DownloadBook(link, name);
        });

        tasks.Add(task);

    }

    Task.WaitAll(tasks.ToArray());
}
#endregion Selenium Scraping

#region Google Cloud OCR
void DigitalizeYizkorBook(string yizkorBookImagesFolder)
{
    /*
     * 1. Add google key to environment variables
     * 2. Activate billing in google api
     */

    //var uri = "https://digitalcollections.nypl.org/items/0199e0f0-3eb4-0133-476c-00505686d14e#/?uuid=035fc5f0-3eb4-0133-8ce7-00505686d14e";
    //var image = Image.FromUri(uri);
    var errors = new List<string>();
    //var images = Directory.GetFiles(@"C:\Users\avraham.kahana\Downloads\Yizkor Book Digitalized");
    var images = Directory.GetFiles(yizkorBookImagesFolder);
    List<string> bookTexts = new List<string>();
    DirectoryInfo outputDir = null;
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
    if (!Directory.Exists("Output"))
        outputDir = Directory.CreateDirectory($"Output {timestamp}");

    var client = ImageAnnotatorClient.Create();

    for (var i = 0; i < images.Count(); i++)
    {
        var image = images[i];

        var googleImage = Google.Cloud.Vision.V1.Image.FromFile(image);
        //var client = ImageAnnotatorClient.Create();
        TextAnnotation response;
        try
        {
            response = client.DetectDocumentText(googleImage);

        }
        catch (Exception ex)
        {
            var errorMsg = $"image [{i + 1}] failed with [{ex.Message}]";
            Console.WriteLine(errorMsg);
            errors.Add(errorMsg);
            continue;
        }

        var pageText = response?.Text ?? string.Empty;
        bookTexts.Add(string.Join(string.Empty, pageText));
        var pageOutputPath = Path.Combine(outputDir.FullName, $"{Path.GetFileNameWithoutExtension(image)}.txt");
        var errorsOutputPath = Path.Combine(outputDir.FullName, "errors.txt");


        List<string> pageTextFinal = new List<string> { $"Page {i + 1}", pageText };
        File.WriteAllText(pageOutputPath, pageText, Encoding.UTF8);
        if (errors.Count > 0)
        {
            File.WriteAllLines(errorsOutputPath, errors, Encoding.UTF8);
        }
        Console.Clear();
        Console.WriteLine($"{i + 1}/{images.Length}");
    }

    File.WriteAllLines($"fullBook {timestamp}.txt", bookTexts, Encoding.UTF8);
}
#endregion Google Cloud OCR