using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ChiikawaDesktopPet.Core;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Dictionary<string, CharacterConfig>))]
[JsonSerializable(typeof(PetProfile))]
[JsonSerializable(typeof(CharacterProfileItem))]
[JsonSerializable(typeof(List<CharacterProfileItem>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(BongoConfig))]
[JsonSerializable(typeof(BongoProfileState))]
[JsonSerializable(typeof(BongoSkinManifest))]
public sealed partial class ConfigJsonContext : JsonSerializerContext
{
}

