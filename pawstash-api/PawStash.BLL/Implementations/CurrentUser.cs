using PawStash.BLL.Interfaces;

namespace PawStash.BLL.Implementations
{
    public class CurrentUser : ICurrentUser
    {
        private string? _email;

        public string Email => _email ?? throw new InvalidOperationException("The request has no signed-in email.");

        public void SignIn(string email)
        {
            _email = email;
        }
    }
}
