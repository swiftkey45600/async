using System.Collections.Concurrent;
using Task2.Models;
using Task2.Services;

namespace Task2;

public class Program
{
    private const int StartId = 1;
    private const int EndId = 500;
    private const int MaxParallelRequests = 3;

    public static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nОтмена запрошена...");
            cts.Cancel();
        };

        try
        {
            using var httpClient = new HttpClient();
            using var semaphore = new SemaphoreSlim(MaxParallelRequests);
            var tasks = new List<Task>();
            var comments = new ConcurrentBag<Comment>();

            int completed = 0;
            int total = EndId - StartId + 1;

            for (int id = StartId; id <= EndId; id++)
            {
                int currentId = id;
                tasks.Add(CommentDownloader.ProcessCommentAsync(
                    currentId,
                    httpClient,
                    semaphore,
                    comments,
                    cts.Token,
                    () =>
                    {
                        int done = Interlocked.Increment(ref completed);
                        Console.WriteLine($"Прогресс: {done}/{total}");
                    }));
            }

            await Task.WhenAll(tasks);

            Console.WriteLine();
            Console.WriteLine($"Успешно загружено комментариев: {comments.Count}");

            var topWords = WordAnalyzer.GetTopWords(comments, 10);

            Console.WriteLine();
            Console.WriteLine("Топ-10 самых частых слов:");
            foreach (var item in topWords)
            {
                Console.WriteLine($"{item.Word} - {item.Count}");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Операция была отменена.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
        }
    }
}
