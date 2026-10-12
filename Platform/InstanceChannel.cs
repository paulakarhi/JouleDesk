using System.Diagnostics;
using System.IO.Pipes;

namespace JouleDesk;

/// <summary>
/// Lets a second launch ask the running instance to show its window. The pipe is per Windows session,
/// like the single-instance mutex, so each signed-in user reaches their own instance.
/// </summary>
internal static class InstanceChannel
{
    private const byte OpenWindowMessage = 1;
    private const int ConnectTimeoutMilliseconds = 1500;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private static readonly string DefaultName = CreateDefaultName();

    public static void Signal(string? name = null)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", name ?? DefaultName, PipeDirection.Out);
            pipe.Connect(ConnectTimeoutMilliseconds);
            pipe.WriteByte(OpenWindowMessage);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // The running instance is busy or closing; the second launch simply exits.
        }
    }

    /// <summary>Calls <paramref name="onSignal"/> for every signal received, until cancelled.</summary>
    public static async Task ListenAsync(Action onSignal, CancellationToken cancellationToken, string? name = null)
    {
        name ??= DefaultName;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ReadTimeout);
                var message = new byte[1];
                await pipe.ReadExactlyAsync(message, timeout.Token).ConfigureAwait(false);
                if (message[0] == OpenWindowMessage)
                    onSignal();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is OperationCanceledException or EndOfStreamException)
            {
                // A client connected but sent nothing; wait for the next one.
            }
            catch (IOException)
            {
                // The pipe could not be created or broke; retry later instead of spinning.
                try
                {
                    await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private static string CreateDefaultName()
    {
        using var process = Process.GetCurrentProcess();
        return $"JouleDesk.Open.{process.SessionId}";
    }
}
