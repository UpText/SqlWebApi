namespace sqlwebapi;
using System;
using System.Security.Cryptography;
using System.Text;

public static class UpHasher
{
    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();

        byte[] bytes = Encoding.UTF8.GetBytes(password);
        byte[] hash = sha.ComputeHash(bytes);

        return Convert.ToBase64String(hash);
    }
}