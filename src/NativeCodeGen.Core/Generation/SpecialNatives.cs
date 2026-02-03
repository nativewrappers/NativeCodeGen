namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Contains native function hashes used for special built-in methods.
/// These are CFX/RedM natives used by generated entity classes.
/// </summary>
public static class SpecialNatives
{
    /// <summary>
    /// GET_PLAYER_SERVER_ID (CFX native) - Gets a player's server ID.
    /// Used by Player.ServerId / Player:getServerId()
    /// </summary>
    public const string GetPlayerServerId = "0x4D97BCC7";

    /// <summary>
    /// NETWORK_GET_NETWORK_ID_FROM_ENTITY - Gets an entity's network ID.
    /// Used by Entity.NetworkId / Entity:getNetworkId()
    /// </summary>
    public const string NetworkGetNetworkIdFromEntity = "0xA11700682F3AD45C";

    /// <summary>
    /// NETWORK_DOES_ENTITY_EXIST_WITH_NETWORK_ID - Checks if an entity exists with a given network ID.
    /// Used by Entity.fromNetworkId() / Entity.fromNetworkId()
    /// </summary>
    public const string NetworkDoesEntityExistWithNetworkId = "0x18A47D074708FD68";

    /// <summary>
    /// NETWORK_GET_ENTITY_FROM_NETWORK_ID - Gets an entity handle from its network ID.
    /// Used by Entity.fromNetworkId() / Entity.fromNetworkId()
    /// </summary>
    public const string NetworkGetEntityFromNetworkId = "0xCE4E5D9B0A4FF560";

    /// <summary>
    /// Mask for ensuring hash values are unsigned 32-bit integers.
    /// </summary>
    public const string HashMask = "0xFFFFFFFF";
}
