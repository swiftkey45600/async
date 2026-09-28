using System.Collections.Concurrent;
using System.Text.Json;
using Task2.Models;

namespace Task2.Services;

public static class CommentDownloader
{
    private const int MaxRetries = 3;

    public static async Task ProcessCommentAsync(
        int id,
        HttpClient httpClient,
        SemaphoreSlim semaphore,
        ConcurrentBag<Comment> comments,
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
}
