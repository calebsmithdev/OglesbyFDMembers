using Mediator;

namespace OglesbyFDMembers.App.Events;

public sealed record PersonCreated(int PersonId) : INotification;