namespace PawStash.Api.Data;

/// <summary>An email allowed to sign in. The email itself is the primary key.</summary>
public class User
{
    public required string Email { get; set; }
}
