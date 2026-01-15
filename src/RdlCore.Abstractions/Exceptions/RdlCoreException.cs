namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// Base exception for RDL Core operations
/// </summary>
public class RdlCoreException(string message, Exception? innerException = null) : Exception(message, innerException) { }
