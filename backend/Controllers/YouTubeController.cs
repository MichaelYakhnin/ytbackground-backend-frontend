using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Xml;
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

        [HttpGet("playFile")]
        public IActionResult PlayFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("File name cannot be empty.");
            }

            // Buffer size configuration based on file size
            const int SmallFileThreshold = 10 * 1024 * 1024;      // 10 MB
            const int LargeFileThreshold = 100 * 1024 * 1024;     // 100 MB
            
            // Buffer sizes for different scenarios
            const int SmallFileBufferSize = 32 * 1024;            // 32 KB for small files
            const int MediumFileBufferSize = 64 * 1024;           // 64 KB for medium files
            const int LargeFileBufferSize = 128 * 1024;           // 128 KB for large files
            
            // Chunk size limits
            const int SmallFileChunkSize = 1 * 1024 * 1024;       // 1 MB for small files
            const int MediumFileChunkSize = 2 * 1024 * 1024;      // 2 MB for medium files
            const int LargeFileChunkSize = 4 * 1024 * 1024;       // 4 MB for large files

            try
            {
                var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userDirectory = Path.Combine(_environment.ContentRootPath, "audio", username);
                var filePath = Path.Combine(userDirectory, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound($"File '{fileName}' not found.");
                }

                var provider = new FileExtensionContentTypeProvider();
                var contentType = provider.TryGetContentType(filePath, out var type) ? type : "audio/mp4";

                var fileInfo = new FileInfo(filePath);
                var fileLength = fileInfo.Length;

                // Determine optimal buffer and chunk sizes based on file size
                int bufferSize, maxChunkSize;
                if (fileLength < SmallFileThreshold)
                {
                    bufferSize = SmallFileBufferSize;
                    maxChunkSize = SmallFileChunkSize;
                }
                else if (fileLength < LargeFileThreshold)
                {
                    bufferSize = MediumFileBufferSize;
                    maxChunkSize = MediumFileChunkSize;
                }
                else
                {
                    bufferSize = LargeFileBufferSize;
                    maxChunkSize = LargeFileChunkSize;
                }

                // Parse Range header if present
                var rangeHeader = Request.Headers["Range"].ToString();
                
                // Configure FileStream with dynamic buffer size
                var fileStreamOptions = new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    BufferSize = bufferSize,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                };

                if (string.IsNullOrEmpty(rangeHeader))
                {
                    try
                    {
                        // No range requested - configure for sequential streaming
                        Response.Headers.Add("Accept-Ranges", "bytes");
                        Response.Headers.Add("Content-Length", fileLength.ToString());
                        
                        var fullContentStream = new FileStream(filePath, fileStreamOptions);
                        
                        return new FileStreamResult(fullContentStream, contentType)
                        {
                            FileDownloadName = fileName,
                            EnableRangeProcessing = true
                        };
                    }
                    catch (IOException ex)
                    {
                        return StatusCode(503, $"Unable to access file: {ex.Message}");
                    }
                }

                // Parse range values
                var range = rangeHeader.Replace("bytes=", "").Split('-');
                if (range.Length != 2)
                {
                    return BadRequest("Invalid range header format.");
                }
                long start, end;
                try
                {
                    start = string.IsNullOrEmpty(range[0]) ? 0 : long.Parse(range[0]);
                    end = string.IsNullOrEmpty(range[1])
                        ? Math.Min(start + maxChunkSize - 1, fileLength - 1)
                        : Math.Min(long.Parse(range[1]), start + maxChunkSize - 1);
                }
                catch (FormatException)
                {
                    return BadRequest("Invalid range values.");
                }

                // Validate range
                if (start < 0 || end < 0)
                {
                    return BadRequest("Range values cannot be negative.");
                }

                if (start >= fileLength || end >= fileLength || start > end)
                {
                    Response.Headers.Add("Content-Range", $"bytes */{fileLength}");
                    return StatusCode(416); // Range Not Satisfiable
                }

                var contentLength = end - start + 1;

                // Set response headers for partial content
                Response.Headers.Add("Accept-Ranges", "bytes");
                Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fileLength}");
                Response.Headers.Add("Content-Length", contentLength.ToString());

                // Create file stream with optimized settings
                try
                {
                    var partialContentStream = new FileStream(filePath, fileStreamOptions);
                    
                    try
                    {
                        partialContentStream.Position = start;
                    }
                    catch (IOException)
                    {
                        partialContentStream.Dispose();
                        return StatusCode(503, "Unable to seek to requested position.");
                    }

                    // Return 206 Partial Content
                    Response.StatusCode = 206;
                    return new FileStreamResult(partialContentStream, contentType)
                    {
                        FileDownloadName = fileName,
                        EnableRangeProcessing = true
                    };
                }
                catch (IOException ex)
                {
                    return StatusCode(503, $"Unable to access file: {ex.Message}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, "Access to file is denied.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}