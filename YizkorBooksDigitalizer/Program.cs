
using System.Net;
using System.Text.RegularExpressions;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium;
using Newtonsoft.Json;
using System.Text;
using Google.Cloud.Vision.V1;
using YizkorBooksDigitalizer.Types.SQLiteDAL;
using System.Diagnostics;
using Serilog;
using System.Globalization;

AllInOne("https://digitalcollections.nypl.org/items/6e01ff60-2e35-0133-5b16-58d385a7bbd0", "zyradow-amshinov-wiskitki");
//DownloadBook("https://digitalcollections.nypl.org/items/54ccda90-5019-0133-0fc4-00505686a51c", out int startingImgId, "bessarabia-1971");

#region Selenium Scraping
void DownloadBook(string uri, out int startingImgId, string placeName = null, bool headless = false)
{
    startingImgId = -1;
    Directory.CreateDirectory(Path.Combine(@"C:\\Users\\avrei\\source\\repos\\YizkorBooksDigitalizer\\Books", placeName));
    var imagesFolder = Directory.CreateDirectory(Path.Combine(@"C:\\Users\\avrei\\source\\repos\\YizkorBooksDigitalizer\\Books", placeName, "Images"));
    var imagesOCRedFolder = Directory.CreateDirectory(Path.Combine(@"C:\\Users\\avrei\\source\\repos\\YizkorBooksDigitalizer\\Books", placeName, "Images OCRed"));

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

    Thread.Sleep(TimeSpan.FromMilliseconds(250));
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
            Console.WriteLine($"{nameof(DownloadBook)}: {placeName} - next click: {numOfClicksOnNext}");
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }
        else
        {
            var lis = wd.FindElements(By.CssSelector(".carousel-widget-list li"));
            var lastLiElement = lis?.LastOrDefault();
            if (lastLiElement == null) return;

            var outers = lis.Select(e => e.GetAttribute("outerHTML")).ToList();
            var lastImgUrl = lis.Where(l => !string.IsNullOrEmpty(l.GetAttribute("title")))?.Last()?.FindElement(By.CssSelector("img"))?.GetAttribute("src");
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
    Thread.Sleep(TimeSpan.FromMilliseconds(250));

    var imgSrcElement = wd.FindElement(By.CssSelector($"#pagediv1 img"));
    if (imgSrcElement == null) return;
    var imgSrc = imgSrcElement.GetAttribute("src");

    wd.Quit();

    #region Get Country using Google Places Detail API

    var country = string.Empty;
    var language = string.Empty;
    //using (var httpClient = new HttpClient())
    //{
    //    var res = httpClient.GetStringAsync($"https://maps.googleapis.com/maps/api/place/findplacefromtext/json?input={placeName}&inputtype=textquery&key=AIzaSyBAmz3I0KOOxz5hvr2WdARyT0S-IGeSQ-w").Result;
    //    var googlePlacesObj = JsonConvert.DeserializeObject<GooglePlaces>(res);
    //    var placeId = googlePlacesObj?.Candidates?.FirstOrDefault()?.place_id ?? string.Empty;

    //https://www.newtonsoft.com/json/help/html/SelectToken.htm
    //    var placeId = (string)JObject.Parse(res)?.SelectToken("candidates[0].place_id");

    //    var detailCallRes = httpClient.GetStringAsync($"https://maps.googleapis.com/maps/api/place/details/json?key=AIzaSyBAmz3I0KOOxz5hvr2WdARyT0S-IGeSQ-w&place_id={placeId}").Result;
    //    country = (string)JObject.Parse(detailCallRes)?.SelectToken("result.address_components[3].long_name");

    //    WORKING!
    //}
    #endregion

    var imgUrl = imgSrc;
    var matchValue = regexId.Match(imgUrl)?.Value;
    var idOnly = matchValue.Replace("id=", string.Empty);
    int.TryParse(idOnly, out int currentImgId);

    var indexesList = new List<int>();
    var imgDownloadLoopIterarions = lastImgId - currentImgId + 1;
    for (var i = 0; i < imgDownloadLoopIterarions; i++)
    {
        indexesList.Add(i);
    }

    var myHttpClient = new HttpClient();
    var startTime = DateTime.Now;
    var downloadCounter = 0;
    Console.WriteLine($"Total items to download: [{indexesList.Count}]");
    var outputFolderPath = Path.Combine(imagesFolder.FullName);
    if (!Directory.Exists(outputFolderPath))
    {
        Directory.CreateDirectory(outputFolderPath);
    }
    var parallelResult = Parallel.ForEachAsync(indexesList, async (idx, ct) =>
    {
        var destinationFilePath = Path.Combine(outputFolderPath, $"{placeName} page {idx + 1}.jpg");
        Interlocked.Increment(ref downloadCounter);
        
        imgUrl = regexId.Replace(imgUrl, $"id={currentImgId + idx}");
        var stream = await myHttpClient.GetStreamAsync(imgUrl);

        if (File.Exists(destinationFilePath))
        {
            return;
        }
        var fileStream = new FileStream(destinationFilePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);
        Console.Clear();
        Console.WriteLine($"{nameof(DownloadBook)} - {placeName}: {downloadCounter}/{imgDownloadLoopIterarions}");
    });
    parallelResult.Wait();
    Console.WriteLine($"Parallel is completed: {parallelResult.IsCompleted} Elapsed time: [{DateTime.Now - startTime}]");

    File.WriteAllText(Path.Combine(outputFolderPath, $"Starting-Image-Id-{placeName}-{(currentImgId - 1)}.txt"), (currentImgId - 1).ToString());
    startingImgId = currentImgId - 1;

    #region Deprecated use of WebClient for scan downloads - using Parallel.Foreach instead, completes job in 1 min instead of 15. Watch out for missed/undonwloaded images. Saw download loop iterations was sometimes lower than the total pages required to download.
    //var totalPages = lastImgId - currentImgId + 1;
    //var page = 0;
    //do
    //{
    //    webClient.DownloadFile(imgUrl, Path.Combine(imagesFolder.FullName, $"{placeName} page {++page}.jpg"));
    //    Thread.Sleep(TimeSpan.FromSeconds(0.05));
    //    imgUrl = regexId.Replace(imgUrl, $"id={++currentImgId}");
    //    Console.Clear();
    //    Console.WriteLine($"{nameof(DownloadBook)} - {placeName}: {page}/{totalPages}");
    //}
    //while (currentImgId <= lastImgId);

    //webClient.Dispose();
    #endregion Deprecated use of WebClient for scan downloads - using Parallel.Foreach instead, completes job in 1 min instead of 15. Watch out for missed/undonwloaded images. Saw download loop iterations was sometimes lower than the total pages required to download.

    //write metadata json
    File.WriteAllText($"{imagesFolder.FullName}/{placeName} metadata.json", JsonConvert.SerializeObject(new { country, language }, Newtonsoft.Json.Formatting.Indented));
    //Directory.Move(dir.FullName, $@"C:\Users\avraham.kahana\Downloads\books\{placeName}");
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

            DownloadBook(link, out int startingImgId, placeName: name);
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

    var imagesFolder = Path.Combine(@"C:\\Users\\avrei\\source\\repos\\YizkorBooksDigitalizer\\Books", yizkorBookImagesFolder, "Images");
    var imagesOCRedFolder = Path.Combine(@"C:\\Users\\avrei\\source\\repos\\YizkorBooksDigitalizer\\Books", yizkorBookImagesFolder, "Images OCRed");

    //var uri = "https://digitalcollections.nypl.org/items/0199e0f0-3eb4-0133-476c-00505686d14e#/?uuid=035fc5f0-3eb4-0133-8ce7-00505686d14e";
    //var image = Image.FromUri(uri);
    var errors = new List<string>();
    //var images = Directory.GetFiles(@"C:\Users\avraham.kahana\Downloads\Yizkor Book Digitalized");
    var images = Directory.GetFiles(imagesFolder);
    List<string> bookTexts = new List<string>();
    DirectoryInfo outputDir = null;
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
    if (!Directory.Exists("Output"))
        outputDir = Directory.CreateDirectory($"Output {yizkorBookImagesFolder} {timestamp}");

    var client = ImageAnnotatorClient.Create();
    var errorsOutputPath = Path.Combine(imagesOCRedFolder, "errors.txt");


    for (var i = 0; i < images.Count(); i++)
    {
        var image = images[i];
        var pageOutputPath = Path.Combine(imagesOCRedFolder, $"{Path.GetFileNameWithoutExtension(image)}.txt");

        if (File.Exists(pageOutputPath))
        {
            continue;
        }

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

        List<string> pageTextFinal = new List<string> { $"Page {i + 1}", pageText };
        File.WriteAllText(pageOutputPath, pageText, Encoding.UTF8);
        if (errors.Count > 0)
        {
            File.WriteAllLines(errorsOutputPath, errors, Encoding.UTF8);
        }
        Console.Clear();
        Console.WriteLine($"{nameof(DigitalizeYizkorBook)}: {i + 1}/{images.Length}");
    }

    var outputFilePlaceName = Path.GetDirectoryName(yizkorBookImagesFolder);
    if (string.IsNullOrEmpty(outputFilePlaceName))
    {
        outputFilePlaceName = yizkorBookImagesFolder;
    }

    File.WriteAllLines($"fullBook {outputFilePlaceName} {timestamp}.txt", bookTexts, Encoding.UTF8);
}
#endregion Google Cloud OCR

#region Database Creator
void GenerateDatabase(string ocredImagesFolder, string dbSchemaTemplateSqlFile, int startingImgId)
{
    var outputFolder = Path.Combine(@"C:\\Users\\avrei\\source\\repos\\YizkorBooksDigitalizer\\Books", Directory.GetParent(ocredImagesFolder).Name);
    var bookName = Directory.GetParent(ocredImagesFolder)?.Name;

    if (string.IsNullOrEmpty(bookName))
    {
        bookName = ocredImagesFolder;
    }
    var dbFileName = Path.Combine(outputFolder, $"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(bookName)}-{startingImgId}.db");
    if (File.Exists(dbFileName))
    {
        File.Delete(dbFileName);
    }

    var sqliteDAL = new DAL(dbFileName);
    var sqlScript = File.ReadAllText(dbSchemaTemplateSqlFile);
    var affectedRows = sqliteDAL.ExecuteNonQuery(sqlScript);
    //turn off journal creation at every non query command
    sqliteDAL.ExecuteNonQuery($"PRAGMA journal_mode=OFF;");

    var bookId = 1;
    int pageId = 0, lineId = 0, wordId = 0;
    var pageFiles = Directory.GetFiles(ocredImagesFolder, "*.txt").Where(f => !f.Contains("errors", StringComparison.InvariantCultureIgnoreCase)).OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split(' ').Last()));
    for (var pageIndex = 0; pageIndex < pageFiles.Count(); pageIndex++)
    {
        var pageFile = pageFiles.ElementAt(pageIndex);
        var pageLines = File.ReadAllLines(pageFile, Encoding.UTF8).Where(l => !l.All(c => char.IsDigit(c))); //leave behind pagination number lines (should be either the last ones (hopefully) or first ones (like hotin case, weird)

        var succeeded = sqliteDAL.Insert($"INSERT INTO Page (Id, BookId, Number) VALUES ({++pageId}, {bookId}, {pageIndex + 1})");
        Console.Clear();
        Console.WriteLine($"{nameof(GenerateDatabase)}: place: {bookName} page {pageIndex + 1}/{pageFiles.Count()}");

        for (var lineIndex = 0; lineIndex < pageLines.Count(); lineIndex++)
        {
            var pageLine = pageLines.ElementAt(lineIndex);
            //if line is pagination number, skip
            if (pageLine.All(c => !char.IsLetter(c)))
            {
                continue;
            }

            var words = pageLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            succeeded = sqliteDAL.Insert($"INSERT INTO Line (Id, PageId, Number) VALUES ({++lineId}, {pageId}, {lineIndex + 1})");
            //Console.WriteLine($"line [{lineIndex + 1}]/[{pageLines.Length}]");

            for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                var word = words[wordIndex];
                if (word.All(c => !char.IsLetter(c)))
                {
                    continue;
                }

                succeeded = sqliteDAL.Insert($"INSERT INTO Word (Id, LineId, Number, Text) VALUES ({++wordId}, {lineId}, {wordIndex + 1}, '{word?.Trim()}')");
                //Console.WriteLine($"word [{wordIndex + 1}]/[{words.Length}]");
            }
        }
    }

    //sqliteDAL.Insert($"INSERT INTO Book (Id, LineId, Number, Text) VALUES ({(lineIndex * pageIndex)}, {lineIndex + 1}, {wordIndex + 1}, '{word?.Trim()}')");
}
#endregion Database Creator

void AllInOne(string nyplBookLink, string placeName)
{
    Serilog.Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", path: "Logs/yizkor-book-digitalizer.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

    Serilog.Log.Logger.Information($"------------------------------- New Run: {DateTime.Now} [{placeName}] ----------------------------------------");

    var stopwatch = new Stopwatch();
    stopwatch.Start();
    //Step 1
    DownloadBook(nyplBookLink, out int startingImgId, placeName: placeName);
    //int startingImgId = 56744864;
    var lastStepElapsedTIme = stopwatch.Elapsed;
    Serilog.Log.Logger.Information($"Step 1 completed - Book scans downloaded - elapsed time: [{stopwatch.Elapsed}]");

    //Step 2
    DigitalizeYizkorBook(placeName);
    Serilog.Log.Logger.Information($"Step 2 completed - Book scans OCRed - elapsed time: [{stopwatch.Elapsed - lastStepElapsedTIme}]");

    //Step 3
    var gitRootFolder = Directory.GetParent(Directory.GetParent(Directory.GetParent(Environment.CurrentDirectory).FullName).FullName).FullName;
    GenerateDatabase(Path.Combine(Directory.GetParent(gitRootFolder).FullName, "Books", placeName, "Images OCRed"), Path.Combine(gitRootFolder, "YizkorBook-schema-template.sql"), startingImgId);
    Serilog.Log.Logger.Information($"Step 3 completed - Database generated - elapsed time: [{stopwatch.Elapsed - lastStepElapsedTIme}]");

    Console.WriteLine($"Total elapsed time: [{stopwatch.Elapsed}]");
    Serilog.Log.Logger.Information($"Job done ! Total elapsed time: [{stopwatch.Elapsed}]");
}