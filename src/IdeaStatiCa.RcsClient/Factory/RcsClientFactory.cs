﻿using IdeaStatiCa.Api.Common;
using IdeaStatiCa.Api.RCS;
using IdeaStatiCa.Plugin;
using IdeaStatiCa.Plugin.Utilities;
using IdeaStatiCa.RcsClient.Client;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace IdeaStatiCa.RcsClient.Factory
{
	public class RcsClientFactory : IRcsClientFactory
	{
		private const string LOCALHOST_URL = "http://127.0.0.1";
		private readonly IPluginLogger pluginLogger;
		private IHttpClientWrapper httpClientWrapper;
		private int port = -1;
		private Process rcsRestApiProcess = null;
		private readonly string setupDir = null;

		public Action<string, int> StreamingLog { get; set; } = null;
		public Action<string> HeartbeatLog { get; set; } = null;

		// Empty string for Rcs.Rest.Obsolete
		protected string API_VERSION_V1 = "1.0";
		protected string Application_name = "IdeaStatiCa.RcsRestApi.exe";

		public RcsClientFactory(string isSetupDir, IPluginLogger pluginLogger = null, IHttpClientWrapper httpClientWrapper = null)
		{
			this.httpClientWrapper = httpClientWrapper;
			this.pluginLogger = pluginLogger == null ? new NullLogger() : pluginLogger;
			this.setupDir = GetRcsRestApiPath(isSetupDir);
			this.pluginLogger.LogDebug($"RcsClientFactory modified setupDir = '{setupDir}'");
		}

		/// <inheritdoc cref="IRcsClientFactory.CreateRcsApiClient()"/>
		public async Task<IRcsApiController> CreateRcsApiClient()
		{
			var (url, processId) = await RunRcsRestApiService();
			pluginLogger?.LogInformation($"Service is running on {url} with process ID {processId}");
			var wrapper = httpClientWrapper ?? new HttpClientWrapper(pluginLogger, url);
			if (StreamingLog != null)
			{
				wrapper.ProgressLogAction = StreamingLog;
			}
			if (HeartbeatLog != null)
			{
				wrapper.HeartBeatLogAction = HeartbeatLog;
			}
			return new RcsApiClient(processId, pluginLogger, wrapper);
		}

		/// <inheritdoc cref="IRcsClientFactory.CreateRcsApiClient(string)"/>
		public async Task<IRcsApiController> CreateRcsApiClient(string url)
		{
			var wrapper = httpClientWrapper ?? new HttpClientWrapper(pluginLogger, url);
			if (StreamingLog != null)
			{
				wrapper.ProgressLogAction = StreamingLog;
			}
			if (HeartbeatLog != null)
			{
				wrapper.HeartBeatLogAction = HeartbeatLog;
			}
			var client = new RcsApiClient(-1, pluginLogger, wrapper);
			return await Task.FromResult<IRcsApiController>(client);
		}

		private async Task<(string, int)> RunRcsRestApiService()
		{
			return await Task.Run<(string, int)>(async () =>
			{
				if (rcsRestApiProcess is null)
				{

					port = PortFinder.FindPort(Constants.MinGrpcPort, Constants.MaxGrpcPort, pluginLogger);

					while (port > 0)
					{
						var directoryName = !string.IsNullOrEmpty(setupDir) ? setupDir : Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
						string apiExecutablePath = Path.Combine(directoryName, Application_name);

						if (!File.Exists(apiExecutablePath))
						{
							pluginLogger.LogWarning($"RcsClientFactory.RunRcsRestApiService : '{apiExecutablePath}' doesn't exist");
						}

						pluginLogger.LogDebug($"Running {apiExecutablePath} on port {port}");
						// Start the REST API executable with the chosen port
						string arguments = $"-port={port}";
						rcsRestApiProcess = new Process();
						rcsRestApiProcess.StartInfo.FileName = apiExecutablePath;
						rcsRestApiProcess.StartInfo.Arguments = arguments;
						rcsRestApiProcess.StartInfo.UseShellExecute = true;
						rcsRestApiProcess.Start();

						// Check if the API process is ready
						var apiUrlBase = new Uri($"{LOCALHOST_URL}:{port}");
						var apiUrlHeartbeat = new Uri(apiUrlBase, RestApiConstants.RestApiHeartbeat);
						var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
						var isApiReady = await WaitForApiToBeReady(apiUrlHeartbeat, cts.Token);

						if (isApiReady && !rcsRestApiProcess.HasExited)
						{
							rcsRestApiProcess.CloseMainWindow();
							pluginLogger.LogInformation($"REST API process started on port {port}.");
							break;
						}
					}

					if (port <= 0)
					{
						pluginLogger.LogError("Failed to start the REST API on an available port.");
					}
				}

				pluginLogger.LogDebug($"Created process with Id {rcsRestApiProcess.Id}");
				return ($"{LOCALHOST_URL}:{port}/api/{API_VERSION_V1}/", rcsRestApiProcess.Id);
			});
		}

		public void Dispose()
		{
			rcsRestApiProcess?.Kill();
			rcsRestApiProcess?.Dispose();
			rcsRestApiProcess = null;
		}

		private string GetRcsRestApiPath(string directory)
		{
			if (string.IsNullOrEmpty(directory))
			{
				// setup dir is not passed
				pluginLogger.LogDebug("GetRcsRestApiPath : setup dir is not passed");

				return directory;
			}

			// IdeaStatiCa.RcsRestApi.exe is expected in net6.0-windows subdir. Do a chact and modified id if it is needed
			string modifiedDir = directory;
			if (!directory.ToLower().Contains("net6.0-windows"))
			{
				pluginLogger.LogDebug("GetRcsRestApiPath trying to find subdir 'net6.0-windows'");
				var dir = Path.Combine(directory, "net6.0-windows");
				if (Directory.Exists(dir))
				{
					pluginLogger.LogDebug($"GetRcsRestApiPath : '{dir}' exists ");
					modifiedDir = dir;
				}
				else
				{
					pluginLogger.LogWarning($"GetRcsRestApiPath : '{dir}' doesn't exist");
				}
			}

			pluginLogger.LogWarning($"GetRcsRestApiPath : returning '{modifiedDir}'");
			return modifiedDir;
		}

		private async Task<bool> WaitForApiToBeReady(Uri apiUrl, CancellationToken cts)
		{
			using (var httpClient = new HttpClient())
			{
				while (!cts.IsCancellationRequested)
				{
					try
					{
						var response = await httpClient.GetAsync(apiUrl, HttpCompletionOption.ResponseHeadersRead, cts);
						if (response.IsSuccessStatusCode)
						{
							return true;
						}
					}
					catch (HttpRequestException ex)
					{
						pluginLogger.LogDebug($"Error waiting for the REST API to be ready.", ex);
					}
					catch (TaskCanceledException)
					{
						pluginLogger.LogDebug("Timeout occur during http call waiting for the REST API to be ready.");
						return false;
					}
					await Task.Delay(3000, cts);
				}
				pluginLogger.LogDebug("Timeout waiting for the REST API to be ready.");
				return false;
			}
		}
	}
}
