namespace c_sharp_jwt.Tests;

public static class TestDatabase
{
    /// <summary>
    /// A private in-memory SQLite database. It lives exactly as long as the connection that opened it, which is
    /// what makes every test class start from an empty schema.
    /// </summary>
    public const string InMemoryConnectionString = "DataSource=:memory:";
}
