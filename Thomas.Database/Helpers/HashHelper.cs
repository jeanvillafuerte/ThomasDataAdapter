﻿using System;
using System.Text.Json;

namespace Thomas.Database
{
    public static class HashHelper
    {
        static JsonSerializerOptions _options = new JsonSerializerOptions() { DefaultBufferSize = 1024, PropertyNamingPolicy = null, WriteIndented = false, MaxDepth = 3, ReadCommentHandling = JsonCommentHandling.Disallow };

        public static int GenerateHash(string query, in object parameters)
        {
            string json = string.Empty;

            if (parameters != null)
                JsonSerializer.Serialize(parameters, _options);

            return GenerateUniqueHash(query + json);
        }

        public static int GenerateUniqueHash(in ReadOnlySpan<char> span)
        {
            return GenerateHash(span);
        }

        public static int GenerateHash(in ReadOnlySpan<char> span)
        {
            unchecked
            {
                int hash = 27;

                foreach (char c in span)
                {
                    hash = (hash * 13) + c;
                }

                return hash;
            }
        }

    }
}
