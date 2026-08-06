using System.IO;
using System.Collections.Concurrent;
using BookTranslatorStudio.Models;

namespace BookTranslatorStudio.Services;

/// <summary>
/// Procesa cualquier cantidad de bloques con concurrencia y reintentos
/// definidos por el usuario. No introduce topes propios.
/// </summary>
public sealed class TranslationCoordinator(
    ITranslationEngine engine)
{
    public async Task RunAsync(
        IReadOnlyList<BookBlock> blocks,
        TranslationRequestFactory requestFactory,
        int concurrentRequests,
        int retryCount,
        int retryDelaySeconds,
        Func<BookBlock, Task> afterBlock,
        CancellationToken cancellationToken)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        var degree = concurrentRequests <= 0
            ? blocks.Count
            : concurrentRequests;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degree,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            blocks,
            options,
            async (block, token) =>
            {
                block.TranslationStatus =
                    TranslationBlockStatus.InProgress;
                block.TranslationError = string.Empty;

                try
                {
                    block.TranslatedText =
                        await TranslateWithPolicyAsync(
                            block,
                            requestFactory,
                            retryCount,
                            retryDelaySeconds,
                            token);

                    block.TranslationStatus =
                        TranslationBlockStatus.Completed;
                    block.TranslatedUtc = DateTime.UtcNow;
                    block.TranslationError = string.Empty;
                }
                catch (OperationCanceledException)
                {
                    block.TranslationStatus =
                        TranslationBlockStatus.Pending;
                    throw;
                }
                catch (Exception exception)
                {
                    block.TranslationStatus =
                        TranslationBlockStatus.Failed;
                    block.TranslationError = exception.Message;
                }
                finally
                {
                    await afterBlock(block);
                }
            });
    }

    private async Task<string> TranslateWithPolicyAsync(
        BookBlock block,
        TranslationRequestFactory requestFactory,
        int retryCount,
        int retryDelaySeconds,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        var attempt = 0;

        while (retryCount == 0 || attempt <= retryCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            block.TranslationAttempts++;

            try
            {
                return await engine.TranslateAsync(
                    requestFactory(block),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastError = exception;

                if (retryCount > 0 && attempt > retryCount)
                {
                    break;
                }

                if (retryDelaySeconds > 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(retryDelaySeconds),
                        cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            "El motor no completó la traducción.",
            lastError);
    }
}

public delegate TranslationRequest TranslationRequestFactory(
    BookBlock block);
