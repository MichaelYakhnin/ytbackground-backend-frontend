using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Xml;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;

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
        private readonly IDistributedCache _cache;

        public YouTubeController(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IDistributedCache cache)
        {
            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = configuration["YouTube:ApiKey"],
                ApplicationName = this.GetType().ToString()
            });

            _youtubeClient = new YoutubeClient();
            _environment = environment;
            _cache = cache;
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
        public async Task<IActionResult> StreamAudio(string videoId, string title, bool saveToFile = false)
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
                    var fileName = $"{title}.mp4";
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

        // Optimized streaming constants
        private const int BufferSize = 16 * 1024;             // 16 KB read buffer
        private const int DefaultChunkSize = 512 * 1024;      // 512 KB initial chunk
        private const int MaxChunkSize = 2 * 1024 * 1024;     // 2 MB max chunk for range

        [HttpGet("playFile")]
        public async Task<IActionResult> PlayFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest("File name cannot be empty.");

            try
            {
                var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userDirectory = Path.Combine(_environment.ContentRootPath, "audio", username);
                var filePath = Path.Combine(userDirectory, fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound($"File '{fileName}' not found.");

                var provider = new FileExtensionContentTypeProvider();
                var contentType = provider.TryGetContentType(filePath, out var type) ? type : "audio/mp4";
                if (type == "video/mp4") type = "audio/mp4";

                var fileInfo = new FileInfo(filePath);
                var fileLength = fileInfo.Length;

                var fileStreamOptions = new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite,
                    BufferSize = BufferSize,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                };

                
                Response.Headers["Content-Type"] = type;
                var rangeHeader = Request.Headers["Range"].ToString();

                if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                {
                    var range = rangeHeader["bytes=".Length..].Split('-', 2);
                    if (!long.TryParse(range[0], out var start)) start = 0;

                    long end = range.Length > 1 && long.TryParse(range[1], out var parsedEnd)
                        ? Math.Min(parsedEnd, Math.Min(start + MaxChunkSize - 1, fileLength - 1))
                        : Math.Min(start + DefaultChunkSize - 1, fileLength - 1);

                    if (start < 0 || end >= fileLength || start > end)
                    {
                        Response.Headers["Content-Range"] = $"bytes */{fileLength}";
                        return StatusCode(416); // Range Not Satisfiable
                    }
                    Response.Headers["Accept-Ranges"] = "bytes";
                    Response.StatusCode = 206; // Partial Content
                    Response.Headers["Content-Range"] = $"bytes {start}-{end}/{fileLength}";
                    Response.Headers["Content-Length"] = (end - start + 1).ToString();

                    var stream = new FileStream(filePath, fileStreamOptions);
                    stream.Seek(start, SeekOrigin.Begin);

                    return new FileStreamResult(stream, type)
                    {
                        EnableRangeProcessing = false,
                        FileDownloadName = fileName
                    };
                }

                // No range header – send full file
                Response.Headers["Content-Length"] = fileLength.ToString();
                var fullStream = new FileStream(filePath, fileStreamOptions);

                return new FileStreamResult(fullStream, type)
                {
                    EnableRangeProcessing = true,
                    FileDownloadName = fileName
                };
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, "Access to file is denied.");
            }
            catch (IOException ex)
            {
                return StatusCode(503, $"Unable to access file: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}