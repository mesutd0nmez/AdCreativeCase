using AdCreativeCase;
using Newtonsoft.Json;

public class Program
{
    internal delegate void ThreadSummary(int imageCount, int imageParallelism, int processingImageId);
    public static CancellationTokenSource tokenSource = new();

    private static async Task Main(string[] args)
    {
        int count = 0, parallelism = 0;
        string outputFolder = "";
        string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Input.json");

        if (File.Exists(jsonFilePath))
        {
            string jsonContent = File.ReadAllText(jsonFilePath);
            var data = JsonConvert.DeserializeObject<InputModel>(jsonContent);
            count = data.Count;
            parallelism = data.Parallelism;
            outputFolder = data.SavePath;
        }
        else
        {
        repeatCount:
            Console.Write("$ Enter the number of images to download: \n< ");


            if (!int.TryParse(Console.ReadLine(), out count))
            {
                Console.WriteLine("$ Incorrect value entry was made. Please try again. \n\n");
                goto repeatCount;
            }

        repeatParallelism:
            Console.Write("$ Enter the maximum parallel download limit: \n< ");
            if (!int.TryParse(Console.ReadLine(), out parallelism))
            {
                Console.WriteLine("$ Incorrect value entry was made. Please try again. \n\n");
                goto repeatParallelism;
            }

            Console.Write("$ \"Enter the save path (default: ./outputs): \n< ");
            outputFolder = Console.ReadLine() ?? "";
            if (string.IsNullOrEmpty(outputFolder))
            {
                outputFolder = $"{AppDomain.CurrentDomain.BaseDirectory}outputs";
            }

        }

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("$ Transaction canceled by user.");
            tokenSource.Cancel();
        };

        try
        {
            await DownloadImages(count, parallelism, outputFolder);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("$ Transaction canceled by user.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("$ Error occurred: " + ex.Message);
        }

        Console.ReadLine();
    }

    static async Task DownloadImages(int count, int parallelism, string outputFolder)
    {
        using var httpClient = new HttpClient();
        var tasks = new List<Task>();
        var token = tokenSource.Token;
        ThreadSummary sum = new ThreadSummary(ProcessSummary);

        var semaphore = new SemaphoreSlim(parallelism);

        var random = new Random();

        for (int i = 1; i <= count; i++)
        {
            await semaphore.WaitAsync();

            var task = Task.Run(async () =>
            {
                try
                {
                    await DownloadImage(httpClient, outputFolder, i, token);
                    sum.Invoke(count, parallelism, i);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            }, token);

            tasks.Add(task);

            await Task.Delay(500);
        }

        await Task.WhenAll(tasks);
    }

    static async Task DownloadImage(HttpClient httpClient, string outputFolder, int index, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            ct.ThrowIfCancellationRequested();

        var url = $"https://picsum.photos/200/300";
        var fileName = $"{outputFolder}\\{index}.png";

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync();

        Directory.CreateDirectory(Path.GetDirectoryName(fileName) ?? "");

        using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        await contentStream.CopyToAsync(fileStream);
    }
    static void ProcessSummary(int imageCount, int imageParallelism, int processingImageId)
    {
        Console.WriteLine("$$$");
        Console.WriteLine($"Downloading {imageCount} images ({imageParallelism} parallel downloads at most)");
        Console.WriteLine($"Progress: {processingImageId}/{imageCount}");
        Console.WriteLine("$$$");
        Console.WriteLine("");
    }
}