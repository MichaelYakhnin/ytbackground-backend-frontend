using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.Collections.Generic;
using System.Security.Claims;
using System.Xml;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YTBackgroundBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class YouTubeController : ControllerBase
    {
        private readonly YouTubeService _youtubeService;
        private readonly IWebHostEnvironment _environment;
        private readonly YoutubeClient _youtubeClient;
        private readonly YoutubeDL _ytDlp;
        private readonly ILogger<YouTubeController> _logger;
        private readonly IConfiguration _configuration;

        public YouTubeController(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<YouTubeController> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _environment = environment;

            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = _configuration["YouTube:ApiKey"],
                ApplicationName = GetType().ToString()
            });

            _youtubeClient = new YoutubeClient();
            _ytDlp = new YoutubeDL();

            var youtubeDlPath = ResolveYoutubeDlPath();
            _ytDlp.YoutubeDLPath = youtubeDlPath;
            _logger.LogInformation("Using yt-dlp binary at {YoutubeDlPath}", youtubeDlPath);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchVideos(string query, int maxResults = 10)
        {
            try
            {
                var searchListRequest = _youtubeService.Search.List("snippet");
                searchListRequest.Q = query;
                searchListRequest.MaxResults = maxResults;

                var searchListResponse = await searchListRequest.ExecuteAsync();

                var videoIds = searchListResponse.Items.Select(item => item.Id.VideoId).ToList();
                var videoDetailsRequest = _youtubeService.Videos.List("contentDetails");
                videoDetailsRequest.Id = string.Join(",", videoIds);

                var videoDetailsResponse = await videoDetailsRequest.ExecuteAsync();

                var videos = searchListResponse.Items.Select(item =>
                {
                    var videoDetails = videoDetailsResponse.Items.FirstOrDefault(v => v.Id == item.Id.VideoId);
                    var duration = videoDetails?.ContentDetails?.Duration;

                    // Parse the duration to TimeSpan and format it as "HH:mm:ss"
                    var formattedDuration = duration != null ? XmlConvert.ToTimeSpan(duration).ToString(@"hh\:mm\:ss") : "00:00:00";

                    return new
                    {
                        Id = item.Id.VideoId,
                        Title = item.Snippet.Title,
                        Author = item.Snippet.ChannelTitle,
                        Duration = formattedDuration,
                        Thumbnails = item.Snippet.Thumbnails,
                        PublishedAt = item.Snippet.PublishedAtDateTimeOffset ?? DateTimeOffset.MinValue
                    };
                });

                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching videos.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("stream")]
        public async Task<IActionResult> StreamAudio(string videoId, string title, bool saveToFile = false)
        {
            try
            {
                if (saveToFile)
                {
                    var username = User.FindFirstValue(ClaimTypes.NameIdentifier);

                    // Create the directory for the user
                    var userDirectory = Path.Combine(_environment.ContentRootPath, "audio", username);
                    Directory.CreateDirectory(userDirectory);
                    _ytDlp.OutputFolder = userDirectory;
                    // a progress handler with a callback that updates a progress bar
                    var progress = new Progress<DownloadProgress>(p =>_logger.LogInformation("Downloading: {p.Progress}", p.Progress));

                    var res = await _ytDlp.RunAudioDownload($"https://www.youtube.com/watch?v={videoId}",
                         progress: progress, overrideOptions: new OptionSet()
                         {
                             Format = "bestaudio/best",  // <-- only best single audio
                             ExtractAudio = true,        // extracts audio
                             AudioFormat = AudioConversionFormat.Mp3,  // or "best"
                             AudioQuality = 0            // best quality
                         }
                    );

                    return ServeFile($"{res.Data}");
                }
                else
                {

                    var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
                    var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    if (streamInfo == null)
                    {
                        return BadRequest("Failed to retrieve audio stream info.");
                    }

                    var stream = await _youtubeClient.Videos.Streams.GetAsync(streamInfo);
                    var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    if (saveToFile)
                    {
                        // Get the username of the current user
                        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);

                        // Create the directory for the user
                        var userDirectory = Path.Combine(_environment.ContentRootPath, "audio", username);
                        Directory.CreateDirectory(userDirectory);
                        // Save the audio to disk
                        var fileName = $"{title}.mp4";
                        var filePath = Path.Combine(userDirectory, fileName);
                        _logger.LogInformation("Saving audio to {filePath}", filePath);
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, // 64 KB buffer
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            memoryStream.WriteTo(fileStream);
                        }

                    }
                    _logger.LogInformation("Streaming audio for video ID {videoId}", videoId);
                    return new FileStreamResult(memoryStream, "audio/mp4")
                    {
                        EnableRangeProcessing = true,
                        FileDownloadName = $"{videoId}.mp4"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while streaming audio.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("videos")]
        public async Task<IActionResult> GetVideosDetails([FromQuery] string ids)
        {
            try
            {
                var videoIds = ids.Split(',');
                var videoDetailsRequest = _youtubeService.Videos.List("snippet,contentDetails");
                videoDetailsRequest.Id = string.Join(",", videoIds);

                var videoDetailsResponse = await videoDetailsRequest.ExecuteAsync();

                var videos = videoDetailsResponse.Items.Select(video =>
                {
                    var duration = video.ContentDetails?.Duration;
                    var formattedDuration = duration != null ? XmlConvert.ToTimeSpan(duration).ToString(@"hh\:mm\:ss") : "00:00:00";

                    return new
                    {
                        Id = video.Id,
                        Title = video.Snippet.Title,
                        Author = video.Snippet.ChannelTitle,
                        Duration = formattedDuration,
                        Thumbnails = video.Snippet.Thumbnails,
                        PublishedAt = video.Snippet.PublishedAt
                    };
                });

                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving video details.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("savedFiles")]
        public IActionResult GetSavedFiles()
        {
            try
            {
                // Get the username of the current user
                var username = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Get the directory for the user
                var userDirectory = Path.Combine(_environment.ContentRootPath, "audio", username);

                // Check if the directory exists
                if (!Directory.Exists(userDirectory))
                {
                    return Ok(new string[] { });
                }

                // Get the files in the directory
                var files = Directory.GetFiles(userDirectory).Select(Path.GetFileName);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving saved files.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("playFile")]
        public IActionResult PlayFile(string fileName)
        {
            try
            {
                return ServeFile(fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while playing file.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }

        }

        private IActionResult ServeFile(string fileName)
        {
            // Get the username of the current user
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get the directory for the user
            var userDirectory = Path.Combine(_environment.ContentRootPath, "audio", username);
            var filePath = Path.Combine(userDirectory, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found.");
            }

            var memoryStream = new MemoryStream();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, // 64 KB buffer
                 FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                fileStream.CopyTo(memoryStream);
            }
            memoryStream.Position = 0;

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileName, out string contentType))
            {
                contentType = "application/octet-stream";
            }

            return new FileStreamResult(memoryStream, contentType)
            {
                EnableRangeProcessing = true,
                FileDownloadName = fileName
            };
        }

        private string ResolveYoutubeDlPath()
        {
            var configuredPath = _configuration["YoutubeDL:BinaryPath"];
            var defaultPath = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp_linux";
            var fallbackPath = string.IsNullOrWhiteSpace(configuredPath) ? defaultPath : configuredPath;

            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string? candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return;
                }

                if (seen.Add(candidate))
                {
                    candidates.Add(candidate);
                }
            }

            AddCandidate(fallbackPath);
            AddCandidate(defaultPath);

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!Path.IsPathRooted(candidate))
                {
                    AddCandidate(Path.Combine(_environment.ContentRootPath, candidate));
                    AddCandidate(Path.Combine(AppContext.BaseDirectory, candidate));
                }
            }

            foreach (var candidate in candidates)
            {
                if (System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }

            _logger.LogWarning("yt-dlp binary not found at configured locations. Falling back to {FallbackPath}", fallbackPath);
            return fallbackPath;
        }
    }
}