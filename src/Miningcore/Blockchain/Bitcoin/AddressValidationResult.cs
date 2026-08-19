namespace Miningcore.Blockchain.Bitcoin;

// Result of address validation against the coin daemon.
// Three states instead of bool: RPC timeout/error is not proof of an
// invalid address, so it must not collapse into the same "false" as
// an explicit invalid response from the daemon.
public enum AddressValidationResult
{
    // Daemon confirmed the address is valid.
    Valid,

    // Daemon confirmed the address is invalid.
    Invalid,

    // No definitive answer after retries (timeout/connection error).
    // Must not be treated as Invalid.
    Unknown
}
