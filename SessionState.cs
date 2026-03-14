using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace AgentFxLineBot
{
    public class SessionState
    {
        NLog.Logger logger = null!;
        public string SessionId { get; set; } = "NA";
        public string LineUserId { get; set; } = "NA";

        public Microsoft.Agents.AI.AgentSession Session { get; set; } = null!;
        public SessionState(string lineUserId, Microsoft.Agents.AI.AgentSession session)
        {
            Console.WriteLine($"建立新 Session: {lineUserId}");
            LineUserId = lineUserId;
            Reset(session);
        }
        public void Reset(Microsoft.Agents.AI.AgentSession session)
        {
            SessionId = $"{LineUserId}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString().Substring(0, 4)}";
            logger = NLog.LogManager.GetLogger(SessionId);
            Session = session;
            Images.Clear();
        }
        public List<AIContent> Images = new List<AIContent>();
        public void LogInput(string msg) => logger.Debug("INPUT\n" + msg);
        public void LogOutput(string msg) => logger.Debug("OUTPUT\n" + msg);

    }
}