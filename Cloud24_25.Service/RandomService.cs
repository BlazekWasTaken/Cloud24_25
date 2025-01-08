using System.Security.Cryptography;

namespace Cloud24_25.Service;

public static class RandomService
{
    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyz0123456789";
        var code = string.Empty;
        for (var i = 0; i < length; i++)
        {
            var number = RandomNumberGenerator.GetInt32(0, chars.Length);
            code += chars[number];
        }
        return code;
    }
}