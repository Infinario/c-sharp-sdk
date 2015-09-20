using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Infinario.Logging
{

    /**
     * Logs all messages to the console.
     */
    public class ConsoleLogger : Logger
    {

        public void Log(Level level, string message)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("infinario [");
            sb.Append(Enum.GetName(typeof(Level), level));
            sb.Append("]: ");
            sb.Append(message);
            Console.WriteLine(sb.ToString());
        }

        public bool IsLevelEnabled(Level level)
        {
            return true;
        }
    }

}
