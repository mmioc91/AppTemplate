namespace AppTemplate.Application.Tests.Samples;

// A port the sample handlers use, so tests can prove validation stops a command before any port is touched.
public interface ISampleStore
{
    void Add(string name);
}
