namespace AppTemplate.Application.Abstractions;

// Mail to send (ADR 0034). Queued in the unit of work: it leaves exactly when the change that caused it is saved, never
// for a change that failed, and is delivered in the background with retries — a use case never waits for a mail server.
public interface IMailQueue
{
    void Enqueue(OutgoingMail mail);
}
