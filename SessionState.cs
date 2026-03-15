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
        public static string ModelName { get; set; } = "Unset";
        public static decimal ModelInTokenRate { get; set; } = 0;
        public static decimal ModelOutTokenRate { get; set; } = 0;
        public long InTokens { get; set; }
        public long OutTokens { get; set; }
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
            InTokens = 0;
            OutTokens = 0;
        }
        public List<AIContent> Images = new List<AIContent>();
        public void LogInput(string msg) => logger.Debug("INPUT\n" + msg);
        public void LogOutput(string msg) => logger.Debug("OUTPUT\n" + msg);
         public void AddTokens(long inTokens, long outTokens)
        {
            InTokens += inTokens;
            OutTokens += outTokens;
        }
        public string TokenUsage => $" 交談累計成本({ModelName}) IN:{InTokens / 1024f:n1}K ({InTokens * ModelInTokenRate:n2} 元), OUT:{OutTokens / 1024f:n1}K ({OutTokens * ModelOutTokenRate:n2}元)";
    }
}