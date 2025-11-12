namespace SSMM_UI.Oauth.X;

public class XUser
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public override string ToString() => $"{Name} (@{Username})";
}