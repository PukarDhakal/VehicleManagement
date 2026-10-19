namespace VehicleManagement.Domain;

public sealed class DomainValidationException(string message) : Exception(message);
