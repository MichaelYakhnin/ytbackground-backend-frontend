using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace YTBackgroundBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IMongoCollection<UserModel> _usersCollection;

        public AuthController(IConfiguration config)
        {
            _config = config;

            // Connection string and database name
            var connectionString = _config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var databaseName = _config["MongoDB:DatabaseName"] ?? "ytdl_new";

            // Create a MongoClient and access the database
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);

            // Get the users collection
            _usersCollection = database.GetCollection<UserModel>("users");
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] UserModelDTO model, [FromQuery] string ApiKey)
        {
            // Validate the model
            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                return BadRequest("Username and password are required.");
            }

            // Check if the token is valid (if provided)
            if (!string.IsNullOrEmpty(ApiKey))
            {
                var jwtKey = _config["Jwt:Key"];
                if (string.IsNullOrEmpty(jwtKey))
                {
                    return Unauthorized("JWT Key is not configured.");
                }

                if (!ApiKey.Equals(jwtKey))
                {
                    return Unauthorized("Invalid API Key.");
                }
            }

            // Check if the username already exists
            var existingUser = _usersCollection.Find(u => u.Username == model.Username).FirstOrDefault();
            if (existingUser != null)
            {
                return BadRequest("Username already exists.");
            }


            // Create a new user
            var newUser = new UserModel
            {
                Username = model.Username,
                Password = model.Password
            };

            // Insert the new user into the database
            _usersCollection.InsertOne(newUser);

            // Generate a JWT token for the new user
            var token = GenerateJwtToken(newUser);

            // Return the token
            return Ok(new { Token = token });
        }


        [HttpPost("login")]
        public IActionResult Login([FromBody] UserModelDTO model)
        {
            // Retrieve user from MongoDB
            var user = _usersCollection.Find(u => u.Username == model.Username && u.Password == model.Password).FirstOrDefault();

            if (user == null)
            {
                return Unauthorized("Invalid credentials.");
            }

            var token = GenerateJwtToken(user);

            return Ok(new { Token = token });
        }

        private string GenerateJwtToken(UserModel user)
        {
            var jwtKey = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key is not configured.");
            }
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, user.Username)
            };

            var token = new JwtSecurityToken(_config["Jwt:Issuer"],
              _config["Jwt:Audience"],
              claims,
              expires: DateTime.Now.AddDays(365),
              signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}