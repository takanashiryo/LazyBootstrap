using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LazyBootstrap.Services
{
    internal static class TaskObservationExtensions
    {
        /// <summary>
        /// Observes a task on a fire-and-forget async void path and logs failures.
        /// </summary>
        internal static void ForgetWithLogging(this Task task, ILogger logger, string errorMessage)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            RunObserved(task, logger, errorMessage ?? string.Empty);
        }

        private static async void RunObserved(Task task, ILogger logger, string errorMessage)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Message}", errorMessage);
            }
        }
    }
}
