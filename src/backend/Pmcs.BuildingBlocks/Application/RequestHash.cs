using System.Security.Cryptography;
using System.Text;

namespace Pmcs.BuildingBlocks.Application;

public static class RequestHash
{
    public static string Create(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
