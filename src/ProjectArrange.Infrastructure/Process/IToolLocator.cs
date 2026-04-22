namespace ProjectArrange.Infrastructure.Process;

public interface IToolLocator
{
    string Resolve(string toolName);
}

