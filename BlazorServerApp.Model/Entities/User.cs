namespace BlazorServerApp.Model.Entities;

/// <summary>
/// Represents an application user with login credentials.
/// </summary>
public class User : IEntity
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Deleted { get; set; }
}
