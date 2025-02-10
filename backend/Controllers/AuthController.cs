using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
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

        public AuthController(IConfiguration config)
        {
            _config = config;
        }

        private static readonly List<UserModel> Users = new List<UserModel>()
        {
            new UserModel { Username = "admin", Password = "password1" }
        };

        // [HttpPost("register")]
        // public IActionResult Register([FromBody] RegisterModel model)
        // {
        //     if (Users.Any(u => u.Username == model.Username))
        //     {
        //         return BadRequest("Username already exists.");
        //     }

        //     var user = new UserModel
        //     {
        //         Username = model.Username,
        //         Password = model.Password // In real app, hash the password
        //     };

        //     Users.Add(user);

        //     return Ok("User registered successfully.");
        // }

        [HttpPost("login")]
        public IActionResult Login([FromBody] UserModel model)
        {
            var user = Users.FirstOrDefault(u => u.Username == model.Username && u.Password == model.Password); // In real app, compare hashed password

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
              expires: DateTime.Now.AddMinutes(60),
              signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}