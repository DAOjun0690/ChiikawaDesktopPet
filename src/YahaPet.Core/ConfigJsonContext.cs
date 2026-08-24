using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YahaPet.Core;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Dictionary<string, CharacterConfig>))]
public sealed partial class ConfigJsonContext : JsonSerializerContext
{
}

