using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class AccountController : BaseApiController
    {
        private readonly DataContext _context;
        private readonly ITokenService _tokenService;

        public AccountController(DataContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("register")] // /api/account/register
        public async Task<ActionResult<UserDTO>> Register(RegisterDTO registerDTO)
        {
           if (await UserExists(registerDTO.UserName)) return BadRequest("Username is taken");

           using var hmac = new HMACSHA512();
           
           var user = new AppUser
           {
                UserName = registerDTO.UserName.ToLower(),
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDTO.Password)),
                PasswordSalt = hmac.Key
           };

           _context.Add(user);
           await _context.SaveChangesAsync();

           return new UserDTO
           {
                UserName = user.UserName,
                Token = _tokenService.CreateToken(user)

           };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(LoginDTO loginDTO)
        {
            var user = await _context.Users.SingleOrDefaultAsync(x =>
                x.UserName == loginDTO.UserName );

            if (user == null) return Unauthorized("Invalid username");

            using var hmac = new HMACSHA512(user.PasswordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDTO.Password));

            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != user.PasswordHash[i]) return Unauthorized("Invalid password");
            }
            
            return new UserDTO
            {
                UserName = user.UserName,
                Token = _tokenService.CreateToken(user)
            };

        }


        public async Task<bool> UserExists(string username)
        {
            return await _context.Users.AnyAsync(x => x.UserName == username.ToLower());
        }
    }
}