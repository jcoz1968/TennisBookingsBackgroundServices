using TennisBookings.Processing;
using TennisBookings.ResultsProcessing;

namespace TennisBookings.Web.BackgroundServices;

public class FileProcessingService : BackgroundService
{
	private readonly ILogger<FileProcessingService> _logger;
	private readonly FileProcessingChannel _fileProcessingChannel;
	private readonly IServiceProvider _serviceProvider;

	public FileProcessingService(
		ILogger<FileProcessingService> logger,
		FileProcessingChannel boundedMessageChannel,
		IServiceProvider serviceProvider)
	{
		_logger = logger;
		_fileProcessingChannel = boundedMessageChannel;
		_serviceProvider = serviceProvider;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await foreach(var fileName in _fileProcessingChannel.ReadAllAsync().WithCancellation(stoppingToken))
		{
			using var scope  =_serviceProvider.CreateScope();
			var processor = scope.ServiceProvider.GetRequiredService<IResultProcessor>();
			try
			{
				await using var stream = File.OpenRead(fileName);
				await processor.ProcessAsync(stream, stoppingToken);
			}
			catch (Exception exc)
			{
				_logger.LogError(exc.Message);
			}
			finally
			{
				File.Delete(fileName);
			}
		}
	}

	internal static class EventIds
	{
		public static readonly EventId StartedProcessing = new(100, "StartedProcessing");
		public static readonly EventId ProcessorStopping = new(101, "ProcessorStopping");
		public static readonly EventId StoppedProcessing = new(102, "StoppedProcessing");
		public static readonly EventId ProcessedMessage = new(110, "ProcessedMessage");
	}

	private static class Log
	{
		private static readonly Action<ILogger, string, Exception?> _processedMessage = LoggerMessage.Define<string>(
			LogLevel.Debug,
			EventIds.ProcessedMessage,
			"Read and processed message with ID '{MessageId}' from the channel.");

		public static void StartedProcessing(ILogger logger) =>
			logger.Log(LogLevel.Trace, EventIds.StartedProcessing, "Started message processing service.");

		public static void ProcessorStopping(ILogger logger) =>
			logger.Log(LogLevel.Information, EventIds.ProcessorStopping, "Message processing stopping due to app termination!");

		public static void StoppedProcessing(ILogger logger) =>
			logger.Log(LogLevel.Trace, EventIds.StoppedProcessing, "Stopped message processing service.");

		public static void ProcessedMessage(ILogger logger, string messageId) =>
			_processedMessage(logger, messageId, null);
	}
}
