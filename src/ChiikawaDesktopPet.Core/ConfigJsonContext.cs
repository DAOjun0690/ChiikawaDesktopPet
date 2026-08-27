using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ChiikawaDesktopPet.Core;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Dictionary<string, CharacterConfig>))]
[JsonSerializable(typeof(PetProfile))]
[JsonSerializable(typeof(CharacterProfileItem))]
[JsonSerializable(typeof(List<CharacterProfileItem>))]
public sealed partial class ConfigJsonContext : JsonSerializerContext
{
}

