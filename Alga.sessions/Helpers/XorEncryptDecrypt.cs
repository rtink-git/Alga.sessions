using System;

namespace Alga.sessions;

internal static partial class Helpers {
    internal static string XorEncryptDecrypt(string input, string key) {
        char[] output = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
            output[i] = (char)(input[i] ^ key[i % key.Length]);
        return new string(output);
    }
}
