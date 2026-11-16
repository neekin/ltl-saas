namespace Ltl.Modules.Transport.Domain;

/// <summary>运输域领域违规（状态守卫、配载规则）。API 映射为 409。</summary>
public sealed class TransportDomainException(string message) : Exception(message);
