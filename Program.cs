using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class Comment
{
    [JsonPropertyName("postId")]
    public int PostId { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";
}

public class Program
{
    private const int StartId = 1;
    private const int EndId = 500;
    private const int MaxParallelRequests = 3;
    private const int MaxRetries = 3;

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
                tasks.Add(ProcessCommentAsync(
                    currentId,
                    httpClient,
                    semaphore,
                    comments,
                    total,
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

            var topWords = GetTopWords(comments, 10);

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

    private static async Task ProcessCommentAsync(
        int id,
        HttpClient httpClient,
        SemaphoreSlim semaphore,
        ConcurrentBag<Comment> comments,
        int total,
        CancellationToken cancellationToken,
        Action onCompleted)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            string url = $"https://jsonplaceholder.typicode.com/comments/{id}";
            string json = await FetchWithRetriesAsync(httpClient, url, MaxRetries, cancellationToken);

            var comment = JsonSerializer.Deserialize<Comment>(json);

            if (comment != null)
            {
                comments.Add(comment);
            }

            onCompleted();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<string> FetchWithRetriesAsync(
        HttpClient client,
        string url,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await client.GetStringAsync(url, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == maxRetries)
                {
                    break;
                }

                Console.WriteLine($"Ошибка запроса {url}. Попытка {attempt}/{maxRetries}. Повтор...");
                await Task.Delay(1000, cancellationToken);
            }
        }

        throw new Exception($"Не удалось загрузить данные по адресу: {url}", lastException);
    }

    private static List<(string Word, int Count)> GetTopWords(IEnumerable<Comment> comments, int topCount)
    {
        return comments
            .SelectMany(comment => SplitWords(comment.Body))
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .GroupBy(word => word)
            .Select(group => (Word: group.Key, Count: group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Word)
            .Take(topCount)
            .ToList();
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Enumerable.Empty<string>();
        }

        return Regex.Split(text.ToLower(), @"[^a-zA-Z]+")
            .Where(word => !string.IsNullOrWhiteSpace(word));
    }
}