using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Net.Http.Headers;
using System.IO;
using System.Xml;

namespace YTBackgroundBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class YouTubeController : ControllerBase
    {
        private readonly YouTubeService _youtubeService;
        private readonly YoutubeClient _youtubeClient;

        public YouTubeController(IConfiguration configuration)
        {
            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = configuration["YouTube:ApiKey"],
                ApplicationName = this.GetType().ToString()
            });

            _youtubeClient = new YoutubeClient();
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
        public async Task<IActionResult> StreamAudio(string videoId)
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

                return new FileStreamResult(memoryStream, new MediaTypeHeaderValue("audio/mp4").MediaType)
                {
                    FileDownloadName = $"{videoId}.mp4"
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}