namespace Fixtures;

public class MixedAccess
{
    public string PublicMethod(string input) => input;
    private string PrivateMethod(string input) => input;
    protected string ProtectedMethod(string input) => input;
    internal string InternalMethod(string input) => input;
}
