using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Claims;

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

        public YouTubeController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = configuration["YouTube:ApiKey"],
                ApplicationName = this.GetType().ToString()
            });

            _youtubeClient = new YoutubeClient();
            _environment = environment;
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
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("stream")]
        public async Task<IActionResult> StreamAudio(string videoId, bool saveToFile = false)
        {
            try
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
                    var fileName = $"{videoId}.mp4";
                    var filePath = Path.Combine(userDirectory, fileName);
                    Console.WriteLine($"Saving audio to {filePath}");
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        memoryStream.WriteTo(fileStream);
                    }

                    //return Ok($"Audio saved to {filePath}");
                }

                return new FileStreamResult(memoryStream, "audio/mp4")
                {
                    FileDownloadName = $"{videoId}.mp4"
                };
            }
            catch (Exception ex)
            {
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
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("playFile")]
        public async Task<IActionResult> PlayFile(string fileName)
        {
            try
            {
                // Get the username of the current user
                var username = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Get the directory for the user
                var userDirectory = Path.Combine(_environment.ContentRootPath, "audio", username);

                // Get the file path
                var filePath = Path.Combine(userDirectory, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound($"File '{fileName}' not found.");
                }

                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(filePath, out var contentType))
                {
                    contentType = "audio/mp4";
                }

                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new FileStreamResult(fileStream, contentType)
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}