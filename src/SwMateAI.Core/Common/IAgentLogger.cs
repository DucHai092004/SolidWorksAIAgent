namespace SwMateAI.Core.Common
{
    public interface IAgentLogger
    {
        void Info(string message);
        void Error(string message);
    }
}
