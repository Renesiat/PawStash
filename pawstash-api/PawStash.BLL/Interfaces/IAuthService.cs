using PawStash.BLL.Results;
using PawStash.Common.Models.DTO.Auth;

namespace PawStash.BLL.Interfaces
{
    public interface IAuthService
    {
        Task<ServiceResult<UserDto>> Login(LoginPostDto loginPostDto);
    }
}
